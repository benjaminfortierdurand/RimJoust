using System.Collections.Generic;
using System.Linq;
using GiddyUp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimJoust.Tournament
{
	public class ArcheryContest : IExposable
	{
		private const int StagingTimeoutTicks = 1800;
		private const int VolleyTicks = 900;
		private const int PauseTicks = 240;
		private const int Volleys = 3;
		private const int LineSpacing = 2;
		private const int MaxTickFailures = 300;

		private bool _active;
		private int _phase;
		private int _ticks;
		private int _volley;
		private int _crowdTicks;
		private IntVec3 _lineCenter = IntVec3.Invalid;
		private List<Pawn> _archers = new List<Pawn>();
		private List<IntVec3> _lineCells = new List<IntVec3>();
		private Dictionary<int, int> _hits = new Dictionary<int, int>();
		private List<KnightLoadout> _loadouts = new List<KnightLoadout>();
		private List<Pawn> _lastRanking = new List<Pawn>();
		private Thing _target;
		private int _tickFailures;

		public bool Active => _active;

		public IReadOnlyList<Pawn> LastRanking => _lastRanking;

		public bool IsArcher(Pawn p) => _active && p != null && _archers.Contains(p);

		public int HitsOf(Pawn p)
			=> p != null && _hits.TryGetValue(p.thingIDNumber, out var n) ? n : 0;

		public static Thing FindTarget(Map map)
			=> map?.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_ArcheryTarget).FirstOrDefault();

		public static Thing FindSpot(Map map)
			=> map?.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_ArcherySpot).FirstOrDefault();

		public bool TryStart(List<Pawn> archers, IntVec3 lineCenter, Thing target, Map map)
		{
			if (_active || map == null || target == null || !target.Spawned) return false;

			var valid = archers?
				.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.RaceProps.Humanlike)
				.Distinct()
				.ToList();
			if (valid == null || valid.Count < 2) return false;

			_archers = valid;
			_target = target;
			_hits.Clear();
			_lastRanking = new List<Pawn>();
			_phase = 0;
			_ticks = 0;
			_volley = 0;
			_tickFailures = 0;

			_lineCenter = lineCenter;
			if (!ComputeLine(lineCenter, map))
			{
				Messages.Message("RimJoust.ArcheryLaneBlocked".Translate(),
					MessageTypeDefOf.RejectInput, historical: false);
				_archers.Clear();
				_target = null;
				return false;
			}
			_loadouts.Clear();
			for (var i = 0; i < _archers.Count; i++)
			{
				_loadouts.Add(GuestKnightUtility.GiveTourneyBow(_archers[i]));
				PrepareArcher(_archers[i], _lineCells[i % _lineCells.Count]);
			}

			_active = true;
			Messages.Message("RimJoust.ArcheryStarted".Translate(_archers.Count),
				new LookTargets(_archers), MessageTypeDefOf.PositiveEvent, historical: false);
			return true;
		}

		private const float MaxShotDistance = 23f;
		private const int MaxLineHalfWidth = 14;

		private bool ComputeLine(IntVec3 center, Map map)
		{
			_lineCells.Clear();
			var dx = _target.Position.x - center.x;
			var dz = _target.Position.z - center.z;
			var perp = System.Math.Abs(dx) >= System.Math.Abs(dz)
				? new IntVec3(0, 0, 1)
				: new IntVec3(1, 0, 0);

			var needed = _archers.Count;
			for (var j = 0; j <= MaxLineHalfWidth && _lineCells.Count < needed; j += LineSpacing)
			{
				TryAddLineCell(center + perp * j, map);
				if (j > 0 && _lineCells.Count < needed)
					TryAddLineCell(center - perp * j, map);
			}
			return _lineCells.Count >= needed;
		}

		private void TryAddLineCell(IntVec3 c, Map map)
		{
			if (!c.InBounds(map) || !c.Standable(map)) return;
			if (c.DistanceTo(_target.Position) > MaxShotDistance) return;
			if (!GenSight.LineOfSight(c, _target.Position, map, skipFirstCell: true)) return;
			_lineCells.Add(c);
		}

		private static void PrepareArcher(Pawn p, IntVec3 spot)
		{
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

		public void RegisterHit(Pawn archer)
		{
			if (!_active || _phase != 1 || archer == null) return;
			if (!_archers.Contains(archer)) return;
			_hits.TryGetValue(archer.thingIDNumber, out var n);
			_hits[archer.thingIDNumber] = n + 1;
			if (_target != null && _target.Map != null)
				MoteMaker.ThrowText(_target.DrawPos, _target.Map, archer.LabelShort + " +1", 2.6f);
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
					Log.Error("[RimJoust] erreur pendant le concours de tir : " + e);
				if (_tickFailures >= MaxTickFailures)
				{
					Log.Error("[RimJoust] le concours de tir plante en boucle, annulation automatique.");
					try { Cancel(); }
					catch { _active = false; }
				}
			}
		}

		private void TickInner()
		{
			_ticks++;

			for (var i = _archers.Count - 1; i >= 0; i--)
			{
				var p = _archers[i];
				if (p != null && p.Spawned && !p.Dead && !p.Downed) continue;
				_archers.RemoveAt(i);
				if (p == null) continue;
				RestoreArcher(p);
				RestoreWeapon(p);
			}

			if (++_crowdTicks >= 250)
			{
				_crowdTicks = 0;
				GatherCrowd();
			}

			if (_archers.Count == 0) { Cancel(); return; }
			if (_target == null || !_target.Spawned)
			{
				Messages.Message("RimJoust.ArcheryTargetGone".Translate(),
					MessageTypeDefOf.NegativeEvent, historical: false);
				Cancel();
				return;
			}

			if (_phase == 0)
			{
				if (_ticks % 30 != 0) return;
				var allThere = true;
				for (var i = 0; i < _archers.Count; i++)
				{
					var p = _archers[i];
					var spot = _lineCells[i % _lineCells.Count];
					if (p.Position == spot) continue;
					allThere = false;
					if (!p.pather.Moving)
						p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, spot), JobCondition.InterruptForced);
				}
				if (allThere || _ticks > StagingTimeoutTicks)
					StartVolley(1);
				return;
			}

			if (_phase == 1)
			{
				if (_ticks % 60 == 0)
				{
					for (var i = 0; i < _archers.Count; i++)
					{
						var p = _archers[i];
						if (p.CurJobDef == JobDefOf.AttackStatic) continue;
						var spot = _lineCells[i % _lineCells.Count];
						if (p.Position != spot && !p.pather.Moving)
						{
							p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, spot), JobCondition.InterruptForced);
							continue;
						}
						if (p.Position == spot)
							StartShooting(p);
					}
				}
				if (_ticks < VolleyTicks) return;
				if (_volley >= Volleys) { Finish(); return; }

				_phase = 2;
				_ticks = 0;
				foreach (var p in _archers)
				{
					var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
					wait.expiryInterval = PauseTicks + 60;
					p.jobs?.StartJob(wait, JobCondition.InterruptForced);
				}
				var leader = Ranked().FirstOrDefault();
				if (leader != null)
					Messages.Message("RimJoust.ArcheryStandings".Translate(_volley, leader.LabelShort, HitsOf(leader)),
						new LookTargets(leader), MessageTypeDefOf.NeutralEvent, historical: false);
				return;
			}

			if (_phase == 2 && _ticks >= PauseTicks)
				StartVolley(_volley + 1);
		}

		private void StartVolley(int volley)
		{
			_volley = volley;
			_phase = 1;
			_ticks = 0;
			Messages.Message("RimJoust.ArcheryVolley".Translate(volley, Volleys),
				MessageTypeDefOf.NeutralEvent, historical: false);
			if (volley == 1 && _target.Map != null)
				JoustSounds.Horn(new TargetInfo(_target.Position, _target.Map));
			foreach (var p in _archers)
				StartShooting(p);
		}

		private void StartShooting(Pawn p)
		{
			var job = JobMaker.MakeJob(JobDefOf.AttackStatic, _target);
			job.expiryInterval = VolleyTicks;
			job.endIfCantShootTargetFromCurPos = true;
			p.jobs?.StartJob(job, JobCondition.InterruptForced);
		}

		private void GatherCrowd()
		{
			var map = _target?.Map;
			var tm = TournamentManager.Instance;
			if (map == null || tm == null || !_lineCenter.IsValid) return;

			var dx = _target.Position.x - _lineCenter.x;
			var dz = _target.Position.z - _lineCenter.z;
			var axis = System.Math.Abs(dx) >= System.Math.Abs(dz)
				? new IntVec3(System.Math.Sign(dx), 0, 0)
				: new IntVec3(0, 0, System.Math.Sign(dz));
			var perp = axis.x != 0 ? new IntVec3(0, 0, 1) : new IntVec3(1, 0, 0);

			var band = new List<IntVec3>();
			for (var o = -MaxLineHalfWidth - 2; o <= MaxLineHalfWidth + 2; o++)
				for (var d = 3; d <= 6; d++)
				{
					var c = _lineCenter + perp * o - axis * d;
					if (c.InBounds(map) && c.Standable(map)) band.Add(c);
				}

			var lineCenter = _lineCenter;
			bool BehindLine(IntVec3 cell)
			{
				var rx = cell.x - lineCenter.x;
				var rz = cell.z - lineCenter.z;
				return rx * axis.x + rz * axis.z <= -1;
			}

			tm.GatherCrowd(map, _target.Position, _lineCenter - axis * 4, band, BehindLine, 3);
		}

		private List<Pawn> Ranked()
			=> _archers
				.OrderByDescending(HitsOf)
				.ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0)
				.ToList();

		private void Finish()
		{
			var ranked = Ranked();
			_lastRanking = ranked;
			var winner = ranked.FirstOrDefault();

			var tally = string.Join(", ", ranked.Select(p => p.LabelShort + " " + HitsOf(p)));

			foreach (var p in _archers) RestoreArcher(p);
			RestoreAllWeapons();
			_active = false;

			if (winner != null)
			{
				if (winner.Map != null)
					JoustSounds.Victory(new TargetInfo(winner.Position, winner.Map));
				Messages.Message("RimJoust.ArcheryWinner".Translate(winner.LabelShort, HitsOf(winner)),
					new LookTargets(winner), MessageTypeDefOf.PositiveEvent, historical: false);
				Messages.Message("RimJoust.ArcheryScores".Translate(tally),
					MessageTypeDefOf.NeutralEvent, historical: false);
			}
			_archers.Clear();
		}

		public void Cancel()
		{
			if (!_active) return;
			_lastRanking = Ranked();
			foreach (var p in _archers) RestoreArcher(p);
			RestoreAllWeapons();
			_archers.Clear();
			_active = false;
			Messages.Message("RimJoust.ArcheryCancelled".Translate(),
				MessageTypeDefOf.NegativeEvent, historical: false);
		}

		private static void RestoreArcher(Pawn p)
		{
			if (p == null) return;
			var data = ExtendedDataStorage.Singleton?.GetExtendedPawnData(p);
			if (data != null) data.canRide = true;

			if (p.Dead || !p.Spawned) return;
			if (p.CurJobDef == JobDefOf.AttackStatic || p.CurJobDef == JobDefOf.Goto || p.CurJobDef == JobDefOf.Wait_Combat)
				p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			if (p.drafter != null && !p.Downed)
				p.drafter.Drafted = false;
		}

		private void RestoreWeapon(Pawn p)
		{
			for (var i = _loadouts.Count - 1; i >= 0; i--)
			{
				if (_loadouts[i]?.knight != p) continue;
				try { GuestKnightUtility.RestoreLance(_loadouts[i]); }
				catch (System.Exception e) { Log.Error("[RimJoust] restitution d'arc : " + e); }
				_loadouts.RemoveAt(i);
			}
		}

		private void RestoreAllWeapons()
		{
			for (var i = _loadouts.Count - 1; i >= 0; i--)
			{
				try { GuestKnightUtility.RestoreLance(_loadouts[i]); }
				catch (System.Exception e) { Log.Error("[RimJoust] restitution d'arc : " + e); }
			}
			_loadouts.Clear();
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref _active, "active");
			Scribe_Values.Look(ref _phase, "phase");
			Scribe_Values.Look(ref _ticks, "ticks");
			Scribe_Values.Look(ref _volley, "volley");
			Scribe_Values.Look(ref _lineCenter, "lineCenter");
			Scribe_References.Look(ref _target, "target");
			Scribe_Collections.Look(ref _archers, "archers", LookMode.Reference);
			Scribe_Collections.Look(ref _lineCells, "lineCells", LookMode.Value);
			Scribe_Collections.Look(ref _hits, "hits", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref _loadouts, "loadouts", LookMode.Deep);
			Scribe_Collections.Look(ref _lastRanking, "lastRanking", LookMode.Reference);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_archers ??= new List<Pawn>();
				_lineCells ??= new List<IntVec3>();
				_hits ??= new Dictionary<int, int>();
				_loadouts ??= new List<KnightLoadout>();
				_lastRanking ??= new List<Pawn>();
				_archers.RemoveAll(p => p == null);
				_lastRanking.RemoveAll(p => p == null);
			}
		}
	}
}
