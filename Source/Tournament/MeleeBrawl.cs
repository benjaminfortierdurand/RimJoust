using System.Collections.Generic;
using System.Linq;
using GiddyUp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimJoust.Tournament
{
	public class MeleeBrawl : IExposable
	{
		private const int StagingTimeoutTicks = 1800;
		private const int RetargetInterval = 30;
		private const float RingRadius = 5.9f;
		private const int CountdownTicks = 180;
		private const int MaxTickFailures = 300;
		private const int MaxFightTicks = 9000;
		private const int FatigueStartTicks = 2500;

		private bool _active;
		private int _phase;
		private int _ticks;
		private int _countdown;
		private int _lastCdSecond = -1;
		private IntVec3 _center = IntVec3.Invalid;
		private List<Pawn> _fighters = new List<Pawn>();
		private List<IntVec3> _ringCells = new List<IntVec3>();
		private List<Pawn> _eliminationOrder = new List<Pawn>();
		private List<KnightLoadout> _loadouts = new List<KnightLoadout>();
		private HashSet<int> _allIds = new HashSet<int>();
		private int _tickFailures;
		private int _crowdTicks;

		public bool Active => _active;

		public bool Covers(Pawn p) => _active && p != null && _allIds.Contains(p.thingIDNumber);

		public float FatigueFactor
		{
			get
			{
				if (!_active || _phase != 2 || _ticks <= FatigueStartTicks) return 1f;
				var over = (_ticks - FatigueStartTicks) / (float)FatigueStartTicks;
				return Mathf.Min(3f, 1f + over * 0.5f);
			}
		}

		public IReadOnlyList<Pawn> Fighters => _fighters;
		public IReadOnlyList<Pawn> EliminationOrder => _eliminationOrder;

		private Map MapInUse => _fighters.FirstOrDefault(f => f?.Map != null)?.Map;

		public static bool HasMeleeGround(Map map)
			=> map != null && map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_MeleeSpot).Count > 0;

		public static IntVec3 FindCenter(Map map, List<Pawn> fallbackPawns)
		{
			var spot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_MeleeSpot).FirstOrDefault();
			if (spot != null) return spot.Position;

			var a = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).FirstOrDefault();
			var b = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotB).FirstOrDefault();
			if (a != null && b != null)
				return new IntVec3((a.Position.x + b.Position.x) / 2, 0, (a.Position.z + b.Position.z) / 2);

			var c = IntVec3.Zero;
			if (fallbackPawns == null || fallbackPawns.Count == 0) return c;
			foreach (var p in fallbackPawns) c += p.Position;
			return new IntVec3(c.x / fallbackPawns.Count, 0, c.z / fallbackPawns.Count);
		}

		public bool TryStart(List<Pawn> fighters, IntVec3 center, Map map)
		{
			if (_active || map == null || fighters == null) return false;

			var valid = fighters
				.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.RaceProps.Humanlike)
				.Distinct()
				.ToList();
			if (valid.Count < 2) return false;

			_fighters = valid;
			_eliminationOrder.Clear();
			_allIds.Clear();
			foreach (var p in _fighters) _allIds.Add(p.thingIDNumber);
			_center = center.IsValid ? center : _fighters[0].Position;
			_arenaSpot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_MeleeSpot)
				.FirstOrDefault(t => t.Position == _center);
			_phase = 0;
			_ticks = 0;
			_countdown = CountdownTicks;
			_lastCdSecond = -1;
			_tickFailures = 0;

			ComputeRing(map);
			_loadouts.Clear();
			for (var i = 0; i < _fighters.Count; i++)
			{
				_loadouts.Add(GuestKnightUtility.GiveTourneyMace(_fighters[i]));
				PrepareFighter(_fighters[i], _ringCells[i % _ringCells.Count]);
			}

			_active = true;
			Messages.Message("RimJoust.MeleeStarted".Translate(_fighters.Count),
				new LookTargets(_fighters), MessageTypeDefOf.PositiveEvent, historical: false);
			return true;
		}

		private Thing _arenaSpot;

		private RimJoust.Buildings.CompMeleeArena ArenaComp
			=> _arenaSpot?.TryGetComp<RimJoust.Buildings.CompMeleeArena>();

		private float ArenaRadius => ArenaComp?.Radius ?? RimJoustSettings.DefMeleeArenaRadius;

		private bool InsideArena(IntVec3 c)
		{
			var comp = ArenaComp;
			if (comp != null) return comp.InArena(c);
			return c.DistanceTo(_center) <= ArenaRadius + 0.5f;
		}

		private void ComputeRing(Map map)
		{
			_ringCells.Clear();
			var ringRadius = Mathf.Min(RingRadius, ArenaRadius - 1f);
			var n = _fighters.Count;
			for (var i = 0; i < n; i++)
			{
				var ang = Mathf.PI * 2f * i / n;
				var c = _center + new IntVec3(
					Mathf.RoundToInt(Mathf.Cos(ang) * ringRadius), 0,
					Mathf.RoundToInt(Mathf.Sin(ang) * ringRadius));
				if (!c.InBounds(map) || !c.Standable(map) || !InsideArena(c))
					c = CellFinder.RandomClosewalkCellNear(_center, map, 3);
				_ringCells.Add(c);
			}
		}

		private static void PrepareFighter(Pawn p, IntVec3 spot)
		{
			if (p.health != null) p.health.overrideDeathOnDownedChance = 0f;

			var data = ExtendedDataStorage.Singleton?.GetExtendedPawnData(p);
			if (data != null)
			{
				if (p.IsMounted() && data.Mount != null) p.Dismount(data.Mount, data, clearReservation: true);
				data.canRide = false;
			}

			if (p.drafter == null) p.drafter = new Pawn_DraftController(p);
			p.drafter.Drafted = true;
			p.jobs?.ClearQueuedJobs();
			p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, spot), JobCondition.InterruptForced);
		}

		public void Tick()
		{
			if (!_active) return;
			try
			{
				TickInner();
				_tickFailures = 0;
			}
			catch (System.Exception e)
			{
				_tickFailures++;
				if (_tickFailures == 1)
					Log.Error("[RimJoust] erreur pendant la mêlée : " + e);
				if (_tickFailures >= MaxTickFailures)
				{
					Log.Error("[RimJoust] la mêlée plante en boucle, annulation automatique.");
					try { Cancel(); }
					catch { _active = false; }
				}
			}
		}

		private void TickInner()
		{
			_ticks++;
			PruneAndEliminate();

			if (++_crowdTicks >= 250)
			{
				_crowdTicks = 0;
				var crowdMap = MapInUse;
				var tm = TournamentManager.Instance;
				if (crowdMap != null && tm != null)
					tm.GatherCrowd(crowdMap, _center, _center, CrowdBand(crowdMap),
						cell => !InsideArena(cell), 2);
			}

			if (_fighters.Count <= 1) { Finish(); return; }

			if (_phase == 0)
			{
				if (_ticks % 30 == 0)
				{
					var allThere = true;
					for (var i = 0; i < _fighters.Count; i++)
					{
						var p = _fighters[i];
						var spot = _ringCells[i % _ringCells.Count];
						if (p.Position.DistanceTo(spot) <= 3f) continue;
						allThere = false;
						if (!p.pather.Moving)
							p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, spot), JobCondition.InterruptForced);
					}
					if (allThere || _ticks > StagingTimeoutTicks)
					{
						_phase = 1;
						_ticks = 0;
						_countdown = CountdownTicks;
						_lastCdSecond = -1;
					}
				}
				return;
			}

			if (_phase == 1)
			{
				_countdown--;
				var sec = _countdown > 0 ? (_countdown + 59) / 60 : 0;
				if (sec != _lastCdSecond)
				{
					_lastCdSecond = sec;
					if (sec > 0) JoustSounds.CountdownTick();
				}
				if (_countdown <= 0)
				{
					_phase = 2;
					_ticks = 0;
					var map = MapInUse;
					if (map != null) JoustSounds.Horn(new TargetInfo(_center, map));
					Messages.Message("RimJoust.MeleeFight".Translate(),
						new LookTargets(_fighters), MessageTypeDefOf.NeutralEvent, historical: false);
					Retarget();
				}
				return;
			}

			if (_ticks == FatigueStartTicks)
				Messages.Message("RimJoust.MeleeFatigue".Translate(),
					new LookTargets(_fighters), MessageTypeDefOf.NeutralEvent, historical: false);

			if (_ticks > MaxFightTicks)
			{
				Log.Warning("[RimJoust] la mêlée dure trop longtemps, on départage au plus vaillant.");
				Pawn best = null;
				var bestHp = -1f;
				foreach (var f in _fighters)
				{
					var hp = f?.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
					if (hp > bestHp) { bestHp = hp; best = f; }
				}
				for (var i = _fighters.Count - 1; i >= 0; i--)
				{
					if (_fighters[i] == best) continue;
					_eliminationOrder.Add(_fighters[i]);
					RestoreFighter(_fighters[i]);
					RestoreWeapon(_fighters[i]);
					_fighters.RemoveAt(i);
				}
				Finish();
				return;
			}

			if (_ticks % RetargetInterval == 0) Retarget();
		}

		private void PruneAndEliminate()
		{
			for (var i = _fighters.Count - 1; i >= 0; i--)
			{
				var p = _fighters[i];
				if (p != null && p.Spawned && !p.Dead && !p.Downed) continue;
				_fighters.RemoveAt(i);
				if (p == null) continue;
				_eliminationOrder.Add(p);
				RestoreFighter(p);
				RestoreWeapon(p);
				if (_fighters.Count >= 1 && _phase == 2)
					Messages.Message("RimJoust.MeleeEliminated".Translate(p.LabelShort, _fighters.Count),
						new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			}
		}

		private void Retarget()
		{
			for (var i = 0; i < _loadouts.Count; i++)
				GuestKnightUtility.EnsureLoanerWeapon(_loadouts[i]);

			foreach (var p in _fighters)
			{
				if (p?.jobs == null) continue;

				if (p.Map != null && !InsideArena(p.Position))
				{
					if (p.CurJobDef != JobDefOf.Goto)
					{
						var back = CellFinder.RandomClosewalkCellNear(_center, p.Map, 3);
						p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobCondition.InterruptForced);
					}
					continue;
				}

				var cur = p.CurJobDef == JobDefOf.AttackMelee ? p.CurJob?.targetA.Thing as Pawn : null;
				if (cur != null && !cur.Dead && !cur.Downed && cur.Spawned && _fighters.Contains(cur)) continue;

				var target = NearestOpponent(p);
				if (target == null) continue;
				var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
				job.expiryInterval = 2000;
				job.killIncappedTarget = false;
				p.jobs.StartJob(job, JobCondition.InterruptForced);
			}
		}

		private List<IntVec3> CrowdBand(Map map)
		{
			var comp = ArenaComp;
			if (comp != null && comp.HasRegion)
			{
				var band = new List<IntVec3>();
				var seen = new HashSet<IntVec3>(comp.RegionCells);
				var frontier = new List<IntVec3>(comp.RegionCells);
				for (var depth = 0; depth < 4; depth++)
				{
					var next = new List<IntVec3>();
					foreach (var c in frontier)
						foreach (var d in GenAdj.CardinalDirections)
						{
							var n = c + d;
							if (!seen.Add(n)) continue;
							if (!n.InBounds(map)) continue;
							next.Add(n);
							if (n.Standable(map)) band.Add(n);
						}
					frontier = next;
				}
				return band;
			}

			var circleBand = new List<IntVec3>();
			var inner = ArenaRadius + 1f;
			var outer = ArenaRadius + 4f;
			var r = Mathf.CeilToInt(outer);
			for (var dx = -r; dx <= r; dx++)
				for (var dz = -r; dz <= r; dz++)
				{
					var c = _center + new IntVec3(dx, 0, dz);
					var d = c.DistanceTo(_center);
					if (d < inner || d > outer) continue;
					if (c.InBounds(map) && c.Standable(map)) circleBand.Add(c);
				}
			return circleBand;
		}

		private Pawn NearestOpponent(Pawn from)
		{
			Pawn best = null;
			var bestDist = float.MaxValue;
			foreach (var other in _fighters)
			{
				if (other == from || other == null || other.Dead || other.Downed || !other.Spawned) continue;
				var d = from.Position.DistanceTo(other.Position);
				if (d < bestDist) { bestDist = d; best = other; }
			}
			return best;
		}

		private void Finish()
		{
			var winner = _fighters.FirstOrDefault();
			foreach (var p in _fighters) RestoreFighter(p);
			RestoreAllWeapons();
			if (winner != null)
			{
				_eliminationOrder.Add(winner);
				if (winner.Map != null)
					JoustSounds.Victory(new TargetInfo(winner.Position, winner.Map));
				Messages.Message("RimJoust.MeleeWinner".Translate(winner.LabelShort),
					new LookTargets(winner), MessageTypeDefOf.PositiveEvent, historical: false);
			}
			_fighters.Clear();
			_active = false;
		}

		public void Cancel()
		{
			if (!_active) return;
			foreach (var p in _fighters) RestoreFighter(p);
			foreach (var p in _eliminationOrder) RestoreFighter(p);
			RestoreAllWeapons();
			_fighters.Clear();
			_active = false;
			Messages.Message("RimJoust.MeleeCancelled".Translate(),
				MessageTypeDefOf.NegativeEvent, historical: false);
		}

		private void RestoreWeapon(Pawn p)
		{
			for (var i = _loadouts.Count - 1; i >= 0; i--)
			{
				if (_loadouts[i]?.knight != p) continue;
				try { GuestKnightUtility.RestoreLance(_loadouts[i]); }
				catch (System.Exception e) { Log.Error("[RimJoust] restitution d'arme de mêlée : " + e); }
				_loadouts.RemoveAt(i);
			}
		}

		private void RestoreAllWeapons()
		{
			for (var i = _loadouts.Count - 1; i >= 0; i--)
			{
				try { GuestKnightUtility.RestoreLance(_loadouts[i]); }
				catch (System.Exception e) { Log.Error("[RimJoust] restitution d'arme de mêlée : " + e); }
			}
			_loadouts.Clear();
		}

		private static void RestoreFighter(Pawn p)
		{
			if (p == null) return;
			if (p.health != null) p.health.overrideDeathOnDownedChance = -1f;

			var data = ExtendedDataStorage.Singleton?.GetExtendedPawnData(p);
			if (data != null) data.canRide = true;

			if (p.Dead || !p.Spawned) return;
			if (p.CurJobDef == JobDefOf.AttackMelee || p.CurJobDef == JobDefOf.Goto)
				p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			if (p.drafter != null && !p.Downed)
				p.drafter.Drafted = false;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref _active, "active");
			Scribe_Values.Look(ref _phase, "phase");
			Scribe_Values.Look(ref _ticks, "ticks");
			Scribe_Values.Look(ref _countdown, "countdown");
			Scribe_Values.Look(ref _center, "center");
			Scribe_References.Look(ref _arenaSpot, "arenaSpot");
			Scribe_Collections.Look(ref _fighters, "fighters", LookMode.Reference);
			Scribe_Collections.Look(ref _ringCells, "ringCells", LookMode.Value);
			Scribe_Collections.Look(ref _eliminationOrder, "eliminationOrder", LookMode.Reference);
			Scribe_Collections.Look(ref _loadouts, "loadouts", LookMode.Deep);
			Scribe_Collections.Look(ref _allIds, "allIds", LookMode.Value);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_fighters ??= new List<Pawn>();
				_ringCells ??= new List<IntVec3>();
				_eliminationOrder ??= new List<Pawn>();
				_loadouts ??= new List<KnightLoadout>();
				_allIds ??= new HashSet<int>();
				_fighters.RemoveAll(p => p == null);
			}
		}
	}
}
