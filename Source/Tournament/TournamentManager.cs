using System.Collections.Generic;
using System.Linq;
using GiddyUp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimJoust.Tournament
{
	public class TournamentManager : GameComponent
	{
		public static bool DebugHorses = false;

		internal static void DbgHorse(Pawn horse, string msg)
		{
			if (!DebugHorses || horse == null) return;
			Messages.Message($"[joust] {horse.LabelShort}: {msg}", horse, MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private bool _active;
		private bool _shownIntro;
		private bool _showRoleGizmos = true;
		private bool _allowGuestArrivals = true;
		private int _lastTournamentTick = -1;
		private List<Pawn> _remaining = new List<Pawn>();
		private List<Pawn> _winners = new List<Pawn>();
		private List<Pawn> _eliminated = new List<Pawn>();
		private Pawn _boutA;
		private Pawn _boutB;
		private int _boutGraceTicks;
		private int _boutTicks;
		private bool _boutCharging;
		private int _betweenBoutTicks;
		private Pawn _clearingLoser;
		private int _clearTicks;
		private bool _squireOnIt;
		private int _roundNumber;
		private int _totalParticipants;
		private Dictionary<int, int> _wins = new Dictionary<int, int>();
		private List<BoutRecord> _history = new List<BoutRecord>();
		private List<string> _log = new List<string>();
		private List<TournamentRecord> _pastTournaments = new List<TournamentRecord>();
		private MeleeBrawl _melee = new MeleeBrawl();
		private ArcheryContest _archery = new ArcheryContest();
		private GrandTournament _grand = new GrandTournament();
		private Pawn _champion;
		private Pawn _queen;
		private Pawn _lord;
		private int _ceremonyTicks;
		private int _ceremonyWaitTicks;
		private bool _lordArrived;
		private IntVec3 _ceremonySeatCell = IntVec3.Invalid;
		private List<Pawn> _oathQueue = new List<Pawn>();
		private Pawn _oathKnight;
		private int _oathPhase;
		private int _oathTicks;
		private int _oathReturned;
		private int _oathTotal;
		private IntVec3 _oathCell = IntVec3.Invalid;
		private HashSet<int> _enrolledGuestIds = new HashSet<int>();
		private List<GuestLoadout> _guestLoadouts = new List<GuestLoadout>();
		private List<KnightLoadout> _knightLoadouts = new List<KnightLoadout>();
		private HashSet<int> _squireIds = new HashSet<int>();
		private HashSet<int> _jousterIds = new HashSet<int>();
		private List<Pawn> _looseHorses = new List<Pawn>();
		private int _horseTicks;
		private int _sweepTicks;
		private int _shooTicks;
		private int _gatherTicks;
		private int _tickFailures;
		private int _wedgeKicks;

		private const int BoutGraceTicks = 150;
		private const int MaxBoutTicks = 5400;
		private const int TransitionTicks = 120;
		private const int GatherTicks = 1800;
		private const int MaxClearWithSquireTicks = 2400;
		private const int MaxClearNoSquireTicks = 180;
		private const int StagingPerpOffset = 6;
		private const int SquirePostOffset = 4;
		private const int StagingSpacing = 2;
		private const int CeremonyTicks = 300;
		private const int CeremonyLineOffset = 3;
		private const int CeremonySpacing = 2;
		private const int OathPauseTicks = 140;
		private const int OathTravelTimeout = 1200;
		private const int PostParadeTicks = 300;
		private const int CeremonyMaxWaitTicks = 3600;
		private const int OathDistance = 4;
		private const int HorseDispatchInterval = 120;
		private const int LaneSweepInterval = 60;
		private const float HorseParkRadius = 3f;
		private const float HorseReachSearchRadius = 12f;
		private const int HorseParkWaitTicks = 5000;

		private const float BoutJoustXp = 4000f;
		private const float BoutWinnerJoustXp = 8000f;
		private const float ChampionJoustXp = 12000f;

		private const int LaneClearHalfWidth = 3;
		private const int LaneClearEndMargin = 2;
		private const int LaneClearPushOut = 5;
		private const int ShooInterval = 45;
		private const string LegacyJoustZoneLabel = "Joute (hors-lice)";

		private const int SpectatorInnerOffset = StagingPerpOffset + 2;
		private const int SpectatorDepth = 4;
		private const int SpectatorEndMargin = 2;
		private const int GatherInterval = 250;
		private const float SpectatorChairRadius = 25f;

		public static TournamentManager Instance { get; private set; }

		public TournamentManager(Game game) { Instance = this; }

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			Instance = this;
			DeleteLegacyJoustZoneAreas();

			if (!_active && (_guestLoadouts.Count > 0 || _enrolledGuestIds.Count > 0 || _knightLoadouts.Count > 0))
				ReleaseGuests();
		}

		public bool Active => _active;

		private static int CooldownTicks => Mathf.Max(0, RimJoustMod.Settings?.tournamentCooldownDays ?? 15) * 60000;
		public bool TournamentOnCooldown
			=> _lastTournamentTick >= 0 && CooldownTicks > 0 && Find.TickManager.TicksGame < _lastTournamentTick + CooldownTicks;
		public int TicksUntilTournament => Mathf.Max(0, _lastTournamentTick + CooldownTicks - Find.TickManager.TicksGame);
		public float DaysUntilTournament => TicksUntilTournament / 60000f;

		public static bool DebugGuests = false;

		public int CurrentRound => _roundNumber;
		public Pawn CurrentBoutA => _boutA;
		public Pawn CurrentBoutB => _boutB;
		public IReadOnlyList<Pawn> RemainingThisRound => _remaining;
		public IReadOnlyList<Pawn> AdvancedThisRound => _winners;
		public IReadOnlyList<Pawn> Eliminated => _eliminated;
		public int TotalParticipants => _totalParticipants;
		public int StillInRunning => _totalParticipants - _eliminated.Count;
		public int WinsOf(Pawn p) => p != null && _wins.TryGetValue(p.thingIDNumber, out var n) ? n : 0;
		public float Progress => _totalParticipants > 1 ? (float)_eliminated.Count / (_totalParticipants - 1) : 0f;
		public IReadOnlyList<BoutRecord> History => _history;
		public IReadOnlyList<string> EventLog => _log;
		public IReadOnlyList<TournamentRecord> PastTournaments => _pastTournaments;
		public MeleeBrawl Melee => _melee;
		public ArcheryContest Archery => _archery;
		public GrandTournament Grand => _grand;
		public Pawn Champion => _champion;
		public Pawn Queen => _queen;
		public bool HasResults => !_active && _champion != null;

		public bool IsEnrolledGuest(Pawn p) => p != null && _enrolledGuestIds.Contains(p.thingIDNumber);
		public bool CanEnrollGuest(Pawn p) => GuestKnightUtility.IsEligibleGuest(p);

		public void ToggleGuestEnrollment(Pawn p)
		{
			if (p == null) return;
			if (!_enrolledGuestIds.Remove(p.thingIDNumber)) _enrolledGuestIds.Add(p.thingIDNumber);
		}

		public Pawn OpponentOf(Pawn p)
		{
			if (!_active || p == null) return null;
			if (p == _boutA) return _boutB;
			if (p == _boutB) return _boutA;
			return null;
		}

		public bool TryStartTournamentFromMap(Map map)
		{
			if (_active) { Reject("RimJoust.TournamentAlreadyRunning".Translate()); return false; }
			if (map == null) return false;

			if (map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).Count == 0
				|| map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotB).Count == 0)
			{
				Reject("RimJoust.NeedFlagsPlaced".Translate());
				return false;
			}

			var anyMarked = false;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
				if (IsJouster(c)) { anyMarked = true; break; }

			var roster = new List<Pawn>();
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || c.Downed || !c.IsMounted() || c.WorkTagIsDisabled(WorkTags.Violent)) continue;
				if (c.ageTracker != null && c.ageTracker.AgeBiologicalYears < 16) continue;
				if (IsSquire(c)) continue;
				if (anyMarked && !IsJouster(c)) continue;
				roster.Add(c);
			}

			var guests = new List<Pawn>();
			foreach (var p in map.mapPawns.AllPawnsSpawned)
				if (IsEnrolledGuest(p) && GuestKnightUtility.IsEligibleGuest(p)
					&& GuestKnightUtility.JoustLevelOf(p) >= GuestKnightUtility.MinJoustLevel)
					guests.Add(p);

			if (roster.Count + guests.Count < 2)
			{
				Reject("RimJoust.NotEnoughKnights".Translate(roster.Count + guests.Count));
				return false;
			}

			roster.AddRange(guests);
			return TryStartTournament(roster, guests);
		}

		public void MaybeShowIntroLetter()
		{
			if (_shownIntro) return;
			_shownIntro = true;
			Find.LetterStack.ReceiveLetter(
				"RimJoust.IntroLetterLabel".Translate(),
				"RimJoust.IntroLetterText".Translate(),
				LetterDefOf.NeutralEvent);
		}

		private bool TryStartCeremony()
		{
			if (!TryGetField(out var a, out var b, out _, out _, out var map)) return false;
			var seat = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_LordSeat).FirstOrDefault();
			if (seat == null) return false;

			_lord = PickLord(map);
			if (_lord != null)
			{
				SeatLord(_lord, seat, Midpoint(a, b));
				SeatConsorts(map, Midpoint(a, b));
			}
			LineUpKnights(seat.Position);
			_ceremonySeatCell = seat.Position;
			_ceremonyWaitTicks = 0;
			_lordArrived = false;
			_ceremonyTicks = CeremonyTicks;
			return true;
		}

		private bool LordSeatedNow()
		{
			if (_lord == null || !_lord.Spawned || _lord.Dead) return true;
			if (!_ceremonySeatCell.IsValid) return true;
			return _lord.Position.DistanceTo(_ceremonySeatCell) <= 2.5f;
		}

		private void SeatConsorts(Map map, IntVec3 focus)
		{
			var seats = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_ConsortSeat);
			if (seats == null || seats.Count == 0) return;
			var rels = _lord?.relations?.DirectRelations;
			if (rels == null) return;

			var partners = new List<Pawn>();
			foreach (var r in rels)
			{
				if (r.def != PawnRelationDefOf.Spouse && r.def != PawnRelationDefOf.Lover
					&& r.def != PawnRelationDefOf.Fiance) continue;
				var o = r.otherPawn;
				if (o == null || o.Dead || o.Downed || !o.Spawned || o.Map != map) continue;
				if (!o.IsFreeColonist || IsParticipant(o) || IsSquire(o)) continue;
				if (o.health == null || !o.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				if (!partners.Contains(o)) partners.Add(o);
			}

			var n = System.Math.Min(partners.Count, seats.Count);
			for (var i = 0; i < n; i++)
				SeatLord(partners[i], seats[i], focus);
		}

		private void CeremonyTick()
		{
			if (!TryGetField(out _, out _, out _, out _, out var map)) return;
			var seat = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_LordSeat).FirstOrDefault();
			if (seat == null) return;
			foreach (var p in AllTracked())
				if (p != null && p.Spawned && p.Map == map)
					p.rotationTracker?.FaceCell(seat.Position);
		}

		private bool TryStartOathParade()
		{
			if (_lord == null || _lord.Dead || !_lord.Spawned) return false;
			if (!TryGetField(out var a, out var b, out _, out _, out var map)) return false;
			var seat = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_LordSeat).FirstOrDefault();
			if (seat == null) return false;

			_oathQueue = _remaining.Where(p => p != null && p.Spawned && !p.Dead && p.Map == map).ToList();
			if (_oathQueue.Count == 0) return false;

			var mid = Midpoint(a, b);
			var dx = System.Math.Sign(mid.x - seat.Position.x);
			var dz = System.Math.Sign(mid.z - seat.Position.z);
			var target = new IntVec3(seat.Position.x + dx * OathDistance, 0, seat.Position.z + dz * OathDistance);
			_oathCell = target.InBounds(map) && target.Standable(map)
				? target
				: CellFinder.RandomClosewalkCellNear(target, map, 2);

			_oathTotal = _oathQueue.Count;
			_oathReturned = 0;
			_oathKnight = null;
			_oathPhase = 0;
			_oathTicks = 0;
			return true;
		}

		private bool OathParadeTick()
		{
			if (_oathKnight == null && (_oathQueue == null || _oathQueue.Count == 0)) return false;

			if (_oathKnight != null && (_oathKnight.Dead || !_oathKnight.Spawned))
			{
				_oathKnight = null;
				_oathPhase = 0;
				_oathTicks = 0;
			}

			if (_oathKnight == null)
			{
				while (_oathQueue.Count > 0)
				{
					var next = _oathQueue[0];
					_oathQueue.RemoveAt(0);
					if (next == null || !next.Spawned || next.Dead || next.Downed) continue;
					_oathKnight = next;
					_oathPhase = 0;
					_oathTicks = 0;
					try
					{
						if (next.drafter != null) next.drafter.Drafted = true;
						next.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, _oathCell), JobCondition.InterruptForced);
					}
					catch (System.Exception e)
					{
						Log.Warning("[RimJoust] " + next.LabelShort + " refuse l'ordre de parade (" + e.Message + "), on le saute.");
						_oathKnight = null;
						continue;
					}
					break;
				}
				if (_oathKnight == null)
				{
					FinishOathParade();
					return true;
				}
			}

			_oathTicks++;
			var k = _oathKnight;
			if (_oathPhase == 0)
			{
				var arrived = k.Position == _oathCell || k.Position.AdjacentTo8WayOrInside(_oathCell);
				if (arrived || _oathTicks > OathTravelTimeout)
				{
					_oathPhase = 1;
					_oathTicks = 0;
					if (k.Map != null)
						MoteMaker.ThrowText(k.DrawPos, k.Map, OathLine(), 4.5f);
				}
			}
			else
			{
				if (_lord != null && _lord.Spawned) k.rotationTracker?.FaceCell(_lord.Position);
				if (_oathTicks >= OathPauseTicks)
				{
					SendKnightToPost(k, _oathReturned);
					_oathReturned++;
					_oathKnight = null;
					_oathPhase = 0;
					_oathTicks = 0;
					if (_oathQueue.Count == 0) FinishOathParade();
				}
			}
			return true;
		}

		private void FinishOathParade()
		{
			_oathQueue.Clear();
			_oathKnight = null;
			_oathPhase = 0;
			_oathTicks = 0;
			_betweenBoutTicks = PostParadeTicks;
		}

		private static string OathLine()
			=> ("RimJoust.Oath" + Rand.RangeInclusive(1, 4)).Translate();

		private void SendKnightToPost(Pawn p, int paradeIndex)
		{
			if (p == null || !p.Spawned || p.jobs == null) return;
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var posts = map.listerThings.ThingsOfDef(RimJoust.RimJoustDefOf.RimJoust_KnightPost);
			var j = System.Math.Max(0, paradeIndex - 2);
			var total = System.Math.Max(1, _oathTotal - 2);
			IntVec3 spot;
			if (posts.Count > 0)
				spot = posts[j % posts.Count].Position;
			else
				spot = Midpoint(a, b) + perp * StagingPerpOffset
					+ axis * Mathf.RoundToInt((j - (total - 1) / 2f) * StagingSpacing);
			var cell = CellFinder.RandomClosewalkCellNear(spot, map, 1);
			try
			{
				p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimJoust] " + p.LabelShort + " refuse l'ordre de retour au poste (" + e.Message + ").");
			}
		}

		private Pawn PickLord(Map map)
		{
			Pawn best = null;
			var bestScore = float.MinValue;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || c.Downed || IsParticipant(c) || IsSquire(c)) continue;
				if (c.health == null || !c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				var score = LordPrestige(c);
				if (score > bestScore) { bestScore = score; best = c; }
			}
			return best;
		}

		private static float LordPrestige(Pawn p)
		{
			var s = 0f;
			var title = p.royalty?.MostSeniorTitle;
			if (title != null) s += 10000f + title.def.seniority;
			s += p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			if (p.ageTracker != null) s += p.ageTracker.AgeBiologicalYears * 0.1f;
			return s;
		}

		private void SeatLord(Pawn lord, Thing seat, IntVec3 focus)
		{
			if (lord.jobs == null) return;
			if (lord.drafter != null) lord.drafter.Drafted = false;
			var chair = FindLordChair(seat);
			var job = chair != null
				? JobMaker.MakeJob(RimJoustDefOf.RimJoust_WatchJoust, chair, focus)
				: JobMaker.MakeJob(RimJoustDefOf.RimJoust_WatchJoust, seat.Position, focus);
			lord.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		private static Thing FindLordChair(Thing seat)
		{
			var map = seat.Map;
			if (map == null) return null;
			foreach (var t in seat.Position.GetThingList(map))
				if (t.def?.building != null && t.def.building.isSittable) return t;
			foreach (var off in GenAdj.AdjacentCells)
			{
				var c = seat.Position + off;
				if (!c.InBounds(map)) continue;
				foreach (var t in c.GetThingList(map))
					if (t.def?.building != null && t.def.building.isSittable) return t;
			}
			return null;
		}

		private void LineUpKnights(IntVec3 seatPos)
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var mid = Midpoint(a, b);
			var toSeat = seatPos - mid;
			var dot = toSeat.x * perp.x + toSeat.z * perp.z;
			var perpToward = dot >= 0 ? perp : new IntVec3(-perp.x, 0, -perp.z);

			var knights = AllTracked().Where(p => p != null && p.Spawned && p.Map == map).ToList();
			for (var i = 0; i < knights.Count; i++)
			{
				var spot = mid + perpToward * CeremonyLineOffset
					+ axis * Mathf.RoundToInt((i - (knights.Count - 1) / 2f) * CeremonySpacing);
				var cell = CellFinder.RandomClosewalkCellNear(spot, map, 1);
				var p = knights[i];
				if (p.drafter != null) p.drafter.Drafted = true;
				p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
			}
		}

		public bool TryGetCeremony(Map map, out string text, out float fade)
		{
			text = null; fade = 0f;
			if (map == null) return false;

			if (_oathKnight != null && _oathPhase == 1 && _oathKnight.MapHeld == map)
			{
				text = "RimJoust.OathHail".Translate(_oathKnight.LabelShortCap);
				fade = 1f;
				return true;
			}

			if (_ceremonyTicks <= 0 || !_lordArrived) return false;
			var onThisMap = (_lord != null && _lord.MapHeld == map) || _remaining.Any(p => p?.MapHeld == map);
			if (!onThisMap) return false;
			text = _lord != null
				? "RimJoust.CeremonyHail".Translate(_lord.LabelShortCap)
				: "RimJoust.CeremonyHailNoName".Translate();
			fade = _ceremonyTicks < 45 ? _ceremonyTicks / 45f : 1f;
			return true;
		}

		public bool TryStartTournament(List<Pawn> participants, List<Pawn> guests = null)
		{
			if (_active) { Reject("RimJoust.TournamentAlreadyRunning".Translate()); return false; }
			if (_melee.Active || _archery.Active || _grand.Active) { Reject("RimJoust.EventBlocked".Translate()); return false; }
			if (participants == null || participants.Count < 2) { Reject("RimJoust.TournamentNeedTwo".Translate()); return false; }
			if (TournamentOnCooldown) { Reject("RimJoust.TournamentCooldown".Translate(Mathf.CeilToInt(DaysUntilTournament))); return false; }

			_remaining = participants.InRandomOrder().ToList();
			_winners = new List<Pawn>();
			_eliminated.Clear();
			_wins.Clear();
			_history.Clear();
			_log.Clear();
			_champion = null;
			_queen = null;
			_lord = null;
			_ceremonyTicks = 0;
			_ceremonyWaitTicks = 0;
			_lordArrived = false;
			_ceremonySeatCell = IntVec3.Invalid;
			_oathQueue.Clear();
			_oathKnight = null;
			_oathPhase = 0;
			_oathTicks = 0;
			_oathReturned = 0;
			_oathTotal = 0;
			_guestLoadouts.Clear();
			_knightLoadouts.Clear();
			_totalParticipants = _remaining.Count;

			if (guests != null)
				foreach (var g in guests)
					if (g != null && g.Spawned && !g.Dead)
						_guestLoadouts.Add(GuestKnightUtility.EquipAndMount(g, g.Map));

			foreach (var p in _remaining)
				if (p != null && p.IsColonist)
					_knightLoadouts.Add(GuestKnightUtility.GiveLance(p));
			LogEvent("RimJoust.LogTournamentStart".Translate(_remaining.Count));
			_boutA = _boutB = null;
			_betweenBoutTicks = 0;
			_boutGraceTicks = 0;
			_boutTicks = 0;
			_clearingLoser = null;
			_clearTicks = 0;
			_squireOnIt = false;
			_looseHorses.Clear();
			_horseTicks = 0;
			_wedgeKicks = 0;
			_roundNumber = 1;
			_active = true;
			_lastTournamentTick = Find.TickManager.TicksGame;

			DeleteLegacyJoustZoneAreas();
			ShooBystandersOffLane();
			GatherSpectators();
			if (RimJoustMod.Settings?.autoOpenWindows ?? true)
				RimJoust.UI.Window_TournamentBracket.Open();

			foreach (var p in _remaining)
				if (p?.drafter != null) p.drafter.Drafted = true;

			Messages.Message("RimJoust.TournamentStarted".Translate(_remaining.Count),
				new LookTargets(_remaining), MessageTypeDefOf.PositiveEvent, historical: false);

			if (_guestLoadouts.Count > 0)
				LessonAutoActivator.TeachOpportunity(RimJoustDefOf.RimJoust_Concept_Bets, OpportunityType.GoodToKnow);

			if (!TryStartCeremony())
				_betweenBoutTicks = GatherTicks;
			return _active;
		}

		public bool IsParticipant(Pawn p)
		{
			if (!_active || p == null) return false;
			return p == _boutA || p == _boutB || _remaining.Contains(p) || _winners.Contains(p);
		}

		public bool IsSquire(Pawn p) => p != null && _squireIds.Contains(p.thingIDNumber);

		public void ToggleSquire(Pawn p)
		{
			if (p == null) return;
			if (!_squireIds.Remove(p.thingIDNumber)) _squireIds.Add(p.thingIDNumber);
		}

		public bool IsJouster(Pawn p) => p != null && _jousterIds.Contains(p.thingIDNumber);

		public void ToggleJouster(Pawn p)
		{
			if (p == null) return;
			if (!_jousterIds.Remove(p.thingIDNumber)) _jousterIds.Add(p.thingIDNumber);
		}

		public bool ShowRoleGizmos => _showRoleGizmos;

		public void ToggleRoleGizmos() => _showRoleGizmos = !_showRoleGizmos;

		public bool AllowGuestArrivals => _allowGuestArrivals;

		public void ToggleGuestArrivals() => _allowGuestArrivals = !_allowGuestArrivals;

		public void CancelTournament()
		{
			if (!_active) return;
			Abort("RimJoust.TournamentCancelled".Translate());
		}

		public static bool ArenaReady(Map map)
			=> map != null && map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).Count > 0;

		private void EnsureKnightLance(Pawn p)
		{
			if (p == null || !p.IsColonist) return;
			for (var i = 0; i < _knightLoadouts.Count; i++)
				if (_knightLoadouts[i]?.knight == p)
				{
					GuestKnightUtility.EnsureLance(_knightLoadouts[i]);
					return;
				}
		}

		public void NotifyKnightEliminated(Pawn knight, Pawn mount)
		{
			if (!_active) return;

			if (mount != null && !mount.Dead && !_looseHorses.Contains(mount)) _looseHorses.Add(mount);
		}

		public override void GameComponentTick()
		{
			if (!_active
				&& (_melee == null || !_melee.Active)
				&& (_archery == null || !_archery.Active)
				&& (_grand == null || !_grand.Active))
				return;

			_melee?.Tick();
			_archery?.Tick();
			_grand?.Tick(this);
			if (!_active) return;
			try
			{
				TournamentTick();
				_tickFailures = 0;
			}
			catch (System.Exception e)
			{
				_tickFailures++;
				if (_tickFailures == 1)
					Log.Error("[RimJoust] erreur pendant le tick du tournoi : " + e);
				if (_tickFailures >= 300)
				{
					Log.Error("[RimJoust] le tournoi plante en boucle, annulation automatique.");
					try { Abort("RimJoust.TournamentCancelled".Translate()); }
					catch { _active = false; }
				}
			}
		}

		private void TournamentTick()
		{
			if (_ceremonyTicks > 0)
			{
				CeremonyTick();
				if (!_lordArrived)
				{
					_lordArrived = LordSeatedNow() || ++_ceremonyWaitTicks >= CeremonyMaxWaitTicks;
					if (!_lordArrived) return;
				}
				if (--_ceremonyTicks == 0 && !TryStartOathParade())
					_betweenBoutTicks = GatherTicks;
				return;
			}

			HorseHandlingTick();

			if (++_shooTicks >= ShooInterval)
			{
				_shooTicks = 0;
				ShooBystandersOffLane();
			}

			if (++_gatherTicks >= GatherInterval)
			{
				_gatherTicks = 0;
				GatherSpectators();
			}

			if (OathParadeTick()) return;

			if ((_boutA != null || _boutB != null) && ++_sweepTicks >= LaneSweepInterval)
			{
				_sweepTicks = 0;
				SweepLaneDebris();
			}

			if (_boutA != null || _boutB != null)
			{
				if (_boutGraceTicks > 0) { _boutGraceTicks--; return; }
				_boutTicks++;
				if (BoutResolved(out var winner)) { ResolveBout(winner); return; }
				if (_boutTicks > MaxBoutTicks) ResolveBout(FallbackWinner());
				return;
			}

			if (_clearingLoser != null)
			{
				_clearTicks++;
				var cleared = !_clearingLoser.Spawned || _clearingLoser.Dead;
				var maxTicks = _squireOnIt ? MaxClearWithSquireTicks : MaxClearNoSquireTicks;
				if (cleared || _clearTicks >= maxTicks)
				{
					if (!cleared) MoveLoserToRecovery(_clearingLoser);
					_clearingLoser = null;
					_clearTicks = 0;
					_betweenBoutTicks = TransitionTicks;
				}
				return;
			}

			if (_betweenBoutTicks > 0)
			{
				_betweenBoutTicks--;
				if (_betweenBoutTicks == 0) StartNextBoutOrAdvance();
				return;
			}

			_wedgeKicks++;
			if (_wedgeKicks >= 20)
			{
				Log.Error("[RimJoust] le tournoi n'arrive plus à avancer malgré les relances, annulation.");
				Abort("RimJoust.TournamentCancelled".Translate());
				return;
			}
			Log.Warning("[RimJoust] tournoi à l'arrêt sans état actif, relance de l'enchaînement (" + _wedgeKicks + "/20).");
			_betweenBoutTicks = TransitionTicks;
		}

		private static bool StillFighting(Pawn p)
			=> p != null && p.Spawned && !p.Dead && p.IsMounted();

		private bool BoutResolved(out Pawn winner)
		{
			winner = null;
			var a = StillFighting(_boutA);
			var b = StillFighting(_boutB);
			if (a && b) return false;
			if (a) { winner = _boutA; return true; }
			if (b) { winner = _boutB; return true; }
			winner = FallbackWinner();
			return true;
		}

		private Pawn FallbackWinner()
		{
			var ha = HealthFrac(_boutA);
			var hb = HealthFrac(_boutB);
			if (_boutA != null && (_boutB == null || ha >= hb)) return _boutA;
			return _boutB;
		}

		private static float HealthFrac(Pawn p)
			=> (p == null || p.Dead || p.health == null) ? -1f : p.health.summaryHealth.SummaryHealthPercent;

		private void ResolveBout(Pawn winner, bool silentWin = false)
		{
			var boutA = _boutA;
			var boutB = _boutB;
			var loser = winner == _boutA ? _boutB : _boutA;
			if (loser != null && !_eliminated.Contains(loser)) _eliminated.Add(loser);
			if (winner != null)
			{
				_winners.Add(winner);
				_wins.TryGetValue(winner.thingIDNumber, out var w);
				_wins[winner.thingIDNumber] = w + 1;
				if (!silentWin)
					Messages.Message("RimJoust.TournamentBoutWon".Translate(winner.LabelShort, loser?.LabelShort ?? "?"),
						new LookTargets(winner), MessageTypeDefOf.PositiveEvent, historical: false);
			}

			_history.Add(new BoutRecord(_roundNumber, boutA, boutB, winner));
			if (winner != null && !silentWin)
				LogEvent("RimJoust.LogBoutResult".Translate(_roundNumber,
					winner.LabelShortCap, loser?.LabelShortCap ?? "?"));

			try
			{
				GrantJoustXp(winner, won: true);
				GrantJoustXp(loser, won: false);
				JoustTraitEffects.GiveBoutLossThought(loser);
				ResolveBet(winner);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimJoust] à-côtés de fin de duel : " + e);
			}

			_boutA = _boutB = null;
			_boutTicks = 0;

			if (loser != null && loser.Spawned && !loser.Dead && loser.Downed)
			{
				_clearingLoser = loser;
				_clearTicks = 0;
				_squireOnIt = TryDispatchSquire(loser, loser.Map);
			}
			else
			{
				if (loser != null && loser.Spawned && !loser.Dead && loser.drafter != null)
					loser.drafter.Drafted = false;
				_betweenBoutTicks = TransitionTicks;
			}

			StagePawns();
		}

		private void StartNextBoutOrAdvance()
		{
			SweepLaneDebris();

			_remaining.RemoveAll(p => p == null || p.Dead || !p.Spawned);

			if (_remaining.Count >= 2)
			{
				_boutA = _remaining[0];
				_boutB = _remaining[1];
				_remaining.RemoveRange(0, 2);

				if (JoustTraitEffects.TryForfeit(_boutA, _boutB, out var quitter))
				{
					var freeWinner = quitter == _boutA ? _boutB : _boutA;
					_boutCharging = false;
					Messages.Message("RimJoust.Forfeit".Translate(quitter.LabelShort, freeWinner.LabelShort),
						new LookTargets(quitter, freeWinner), MessageTypeDefOf.NeutralEvent, historical: false);
					LogEvent("RimJoust.LogForfeit".Translate(_roundNumber, freeWinner.LabelShortCap, quitter.LabelShortCap));
					ResolveBout(freeWinner, silentWin: true);
					return;
				}

				_boutGraceTicks = BoutGraceTicks;
				_boutTicks = 0;
				_boutCharging = false;
				_squireOnIt = false;
				bool started;
				try
				{
					EnsureGuestMounted(_boutA);
					EnsureGuestMounted(_boutB);
					EnsureKnightLance(_boutA);
					EnsureKnightLance(_boutB);
					started = JoustStarter.TryStart(_boutA, _boutB);
				}
				catch (System.Exception e)
				{
					Log.Error("[RimJoust] lancement du duel : " + e);
					started = false;
				}
				if (!started)
				{
					_boutA = _boutB = null;
					Abort("RimJoust.TournamentAborted".Translate());
					return;
				}
				_wedgeKicks = 0;
				LogEvent("RimJoust.LogBoutStart".Translate(_roundNumber, _boutA.LabelShortCap, _boutB.LabelShortCap));
				StagePawns();
				return;
			}

			if (_remaining.Count == 1)
			{
				var bye = _remaining[0];
				_winners.Add(bye);
				_history.Add(new BoutRecord(_roundNumber, bye, null, bye, bye: true));
				LogEvent("RimJoust.LogBye".Translate(bye.LabelShortCap));
				Messages.Message("RimJoust.TournamentBye".Translate(bye.LabelShort),
					new LookTargets(bye), MessageTypeDefOf.NeutralEvent, historical: false);
				_remaining.Clear();
			}

			if (_winners.Count >= 2)
			{
				_roundNumber++;
				_remaining = _winners;
				_winners = new List<Pawn>();
				LogEvent("RimJoust.LogNewRound".Translate(_roundNumber, _remaining.Count));
				Messages.Message("RimJoust.TournamentNewRound".Translate(_roundNumber, _remaining.Count),
					new LookTargets(_remaining), MessageTypeDefOf.NeutralEvent, historical: false);
				StartNextBoutOrAdvance();
				return;
			}

			if (_winners.Count == 1) { DeclareChampion(_winners[0]); return; }

			Abort("RimJoust.TournamentNoChampion".Translate());
		}

		private void DeclareChampion(Pawn champion)
		{
			_active = false;
			Step(ReleaseAllParticipants);
			Step(ReleaseSquires);
			Step(ReleaseHorses);
			Step(RestoreCanRideAll);
			Step(ReleaseGuests);
			InvalidateFieldCache();
			_boutA = _boutB = null;
			_clearingLoser = null;
			_oathQueue.Clear();
			_oathKnight = null;
			_remaining.Clear();
			_winners.Clear();
			_champion = champion;
			LogEvent("RimJoust.LogChampion".Translate(champion?.LabelShortCap ?? "?"));

			var grandRunning = _grand != null && _grand.Active;

			if (grandRunning)
			{
				if (champion != null)
					Messages.Message("RimJoust.GrandJoustEventWon".Translate(champion.LabelShort),
						new LookTargets(champion), MessageTypeDefOf.PositiveEvent, historical: false);
			}
			else if (champion != null && !champion.Dead)
				Find.LetterStack.ReceiveLetter(
					"RimJoust.TournamentChampionLetterLabel".Translate(),
					"RimJoust.TournamentChampionLetterText".Translate(champion.LabelShort) + "\n\n" + BuildRecapText(),
					LetterDefOf.PositiveEvent,
					new LookTargets(champion));
			else
				Find.LetterStack.ReceiveLetter(
					"RimJoust.TournamentNoChampionLetterLabel".Translate(),
					"RimJoust.TournamentNoChampion".Translate(),
					LetterDefOf.NeutralEvent);

			if (!grandRunning && champion != null && !champion.Dead && RimJoustDefOf.RimJoust_TournamentChampion != null)
				champion.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoustDefOf.RimJoust_TournamentChampion);

			if (champion != null && !champion.Dead && champion.Spawned)
			{
				try
				{
					JoustSounds.Victory(new TargetInfo(champion.Position, champion.Map));
					JoustSkill.Learn(champion, ChampionJoustXp, direct: true);

					if (!grandRunning)
					{
						if (RimJoustDefOf.RimJoust_ChampionRenown != null && champion.health != null
							&& !champion.health.hediffSet.HasHediff(RimJoustDefOf.RimJoust_ChampionRenown))
							champion.health.AddHediff(HediffMaker.MakeHediff(RimJoustDefOf.RimJoust_ChampionRenown, champion));

						CrownQueenOfLoveAndBeauty(champion);
					}
				}
				catch (System.Exception e)
				{
					Log.Error("[RimJoust] honneurs du champion : " + e);
				}
			}

			if (!grandRunning) Step(RecordTournament);
		}

		public bool TryStartGrandFromMap(Map map)
		{
			if (map == null) return false;
			if (_active || _melee.Active || _archery.Active || _grand.Active)
			{
				Reject("RimJoust.EventBlocked".Translate());
				return false;
			}
			if (TournamentOnCooldown)
			{
				Reject("RimJoust.TournamentCooldown".Translate(Mathf.CeilToInt(DaysUntilTournament)));
				return false;
			}

			var missing = new List<string>();
			if (map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).Count == 0
				|| map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotB).Count == 0)
				missing.Add("RimJoust.GrandNeedLists".Translate());
			if (map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_MeleeSpot).Count == 0)
				missing.Add("RimJoust.GrandNeedMelee".Translate());
			if (ArcheryContest.FindSpot(map) == null || ArcheryContest.FindTarget(map) == null)
				missing.Add("RimJoust.GrandNeedArchery".Translate());
			if (missing.Count > 0)
			{
				Reject("RimJoust.GrandMissing".Translate(string.Join(", ", missing)));
				return false;
			}

			var anyMarked = false;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
				if (IsJouster(c)) { anyMarked = true; break; }

			var fighters = new List<Pawn>();
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || c.Downed || !c.IsMounted() || c.WorkTagIsDisabled(WorkTags.Violent)) continue;
				if (c.ageTracker != null && c.ageTracker.AgeBiologicalYears < 16) continue;
				if (IsSquire(c)) continue;
				if (anyMarked && !IsJouster(c)) continue;
				fighters.Add(c);
			}
			if (fighters.Count < 2)
			{
				Reject("RimJoust.NotEnoughKnights".Translate(fighters.Count));
				return false;
			}

			return _grand.TryStart(this, map, fighters);
		}

		public void FinishGrandTournament(Pawn champion, List<Pawn> standings, Dictionary<int, int> points, int participants)
		{
			_active = false;
			_champion = champion;
			LogEvent("RimJoust.LogChampion".Translate(champion?.LabelShortCap ?? "?"));

			var sb = new System.Text.StringBuilder();
			sb.Append("RimJoust.GrandStandingsHeader".Translate());
			for (var i = 0; i < standings.Count; i++)
			{
				var p = standings[i];
				if (p == null) continue;
				points.TryGetValue(p.thingIDNumber, out var pts);
				sb.AppendLine();
				sb.Append((i + 1) + ". " + p.LabelShortCap + " - " + pts + " pts");
			}

			if (champion != null && !champion.Dead)
				Find.LetterStack.ReceiveLetter(
					"RimJoust.GrandChampionLetterLabel".Translate(),
					"RimJoust.GrandChampionLetterText".Translate(champion.LabelShort) + "\n\n" + sb,
					LetterDefOf.PositiveEvent,
					new LookTargets(champion));

			if (champion != null && !champion.Dead && champion.Spawned)
			{
				try
				{
					JoustSounds.Victory(new TargetInfo(champion.Position, champion.Map));
					JoustSkill.Learn(champion, ChampionJoustXp, direct: true);

					if (RimJoustDefOf.RimJoust_TournamentChampion != null)
						champion.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoustDefOf.RimJoust_TournamentChampion);

					if (RimJoustDefOf.RimJoust_ChampionRenown != null && champion.health != null
						&& !champion.health.hediffSet.HasHediff(RimJoustDefOf.RimJoust_ChampionRenown))
						champion.health.AddHediff(HediffMaker.MakeHediff(RimJoustDefOf.RimJoust_ChampionRenown, champion));

					CrownQueenOfLoveAndBeauty(champion);
				}
				catch (System.Exception e)
				{
					Log.Error("[RimJoust] honneurs du grand champion : " + e);
				}
			}

			Step(() =>
			{
				if (champion == null) return;
				_pastTournaments.Add(new TournamentRecord
				{
					date = CurrentDateString(champion),
					championName = champion.LabelShortCap,
					championFaction = champion.Faction != null && !champion.Faction.IsPlayer ? champion.Faction.Name : null,
					queenName = _queen?.LabelShortCap,
					kind = "grand",
					participants = participants,
					rounds = 3,
				});
				if (_pastTournaments.Count > MaxPastTournaments)
					_pastTournaments.RemoveRange(0, _pastTournaments.Count - MaxPastTournaments);
			});

			_lastTournamentTick = Find.TickManager.TicksGame;
		}

		private const int MaxPastTournaments = 50;

		private void RecordTournament()
		{
			if (_champion == null) return;
			_pastTournaments.Add(new TournamentRecord
			{
				date = CurrentDateString(_champion),
				championName = _champion.LabelShortCap,
				championFaction = _champion.Faction != null && !_champion.Faction.IsPlayer ? _champion.Faction.Name : null,
				queenName = _queen?.LabelShortCap,
				participants = _totalParticipants,
				rounds = _roundNumber,
			});
			if (_pastTournaments.Count > MaxPastTournaments)
				_pastTournaments.RemoveRange(0, _pastTournaments.Count - MaxPastTournaments);
		}

		private static string CurrentDateString(Pawn reference)
		{
			var map = reference?.MapHeld ?? Find.AnyPlayerHomeMap;
			var longLat = map != null ? Find.WorldGrid.LongLatOf(map.Tile) : default;
			return GenDate.DateFullStringAt(GenTicks.TicksAbs, longLat);
		}

		private string BuildRecapText()
		{
			var maxRound = 0;
			foreach (var r in _history) if (r.round > maxRound) maxRound = r.round;
			if (maxRound == 0) return "";

			var sb = new System.Text.StringBuilder();
			sb.Append("RimJoust.RecapTitle".Translate());
			for (var round = 1; round <= maxRound; round++)
			{
				var boutsThisRound = 0;
				foreach (var r in _history) if (r.round == round) boutsThisRound++;
				var isFinal = round == maxRound && maxRound > 1 && boutsThisRound == 1;
				sb.AppendLine();
				sb.AppendLine();
				sb.Append(isFinal ? "RimJoust.RecapFinal".Translate() : "RimJoust.RecapRound".Translate(round));
				foreach (var rec in _history)
				{
					if (rec.round != round) continue;
					sb.AppendLine();
					if (rec.bye)
						sb.Append("  " + "RimJoust.RecapBye".Translate(RecapName(rec.a)));
					else
						sb.Append("  " + "RimJoust.RecapBout".Translate(RecapName(rec.winner), RecapName(rec.Loser)));
				}
			}
			return sb.ToString();
		}

		private static string RecapName(Pawn p) => p?.LabelShortCap.ToString() ?? "?";

		private static void GrantJoustXp(Pawn p, bool won)
		{
			if (p == null || p.Dead || p.skills == null) return;
			JoustSkill.Learn(p, won ? BoutWinnerJoustXp : BoutJoustXp, direct: true);
		}

		private const int CrownDislikeFloor = -10;

		private void CrownQueenOfLoveAndBeauty(Pawn champion)
		{
			var queen = PickQueenOfLoveAndBeauty(champion, out var jilted, out var scandal);
			if (queen == null) return;
			_queen = queen;

			var kind = scandal
				? RimJoust.UI.CoronationKind.Scandal
				: (IsLovePartner(champion, queen) ? RimJoust.UI.CoronationKind.Wife : RimJoust.UI.CoronationKind.Admiration);

			LogEvent("RimJoust.LogQueen".Translate(champion.LabelShortCap, queen.LabelShortCap));

			if (RimJoustDefOf.RimJoust_CrownedQueen != null)
				queen.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoustDefOf.RimJoust_CrownedQueen);

			if (RimJoustDefOf.RimJoust_TourneyFavor != null)
			{
				queen.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoustDefOf.RimJoust_TourneyFavor, champion);
				champion.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoustDefOf.RimJoust_TourneyFavor, queen);
			}

			if (scandal && jilted != null)
				ApplyScandal(champion, queen, jilted);

			Find.LetterStack.ReceiveLetter(
				"RimJoust.QueenLetterLabel".Translate(),
				"RimJoust.QueenLetterText".Translate(champion.LabelShort, queen.LabelShort),
				LetterDefOf.PositiveEvent,
				new LookTargets(new List<Pawn> { champion, queen }));

			if (RimJoustMod.Settings?.autoOpenWindows ?? true)
				RimJoust.UI.Window_Coronation.Show(champion, queen, scandal ? jilted : null, kind);
		}

		private static bool IsLovePartner(Pawn a, Pawn b)
			=> b != null && LovePartnerRelationUtility.ExistingLovePartner(a, allowDead: false) == b;

		private static void ApplyScandal(Pawn champion, Pawn queen, Pawn jilted)
		{
			if (jilted == null || jilted.Dead) return;
			var mem = jilted.needs?.mood?.thoughts?.memories;
			if (mem == null) return;
			if (RimJoustDefOf.RimJoust_PassedOver != null)
				mem.TryGainMemory(RimJoustDefOf.RimJoust_PassedOver);
			if (RimJoustDefOf.RimJoust_JiltedAtTourney != null)
				mem.TryGainMemory(RimJoustDefOf.RimJoust_JiltedAtTourney, champion);
			if (RimJoustDefOf.RimJoust_RivalCrowned != null)
				mem.TryGainMemory(RimJoustDefOf.RimJoust_RivalCrowned, queen);
		}

		private static Pawn PickQueenOfLoveAndBeauty(Pawn champion, out Pawn jilted, out bool scandal)
		{
			jilted = null; scandal = false;
			var map = champion.MapHeld;
			if (map == null) return null;

			var partner = LovePartnerRelationUtility.ExistingLovePartner(champion, allowDead: false);
			var partnerValid = partner != null && partner.Spawned && partner.Map == map && partner.IsFreeColonist
				&& !partner.Dead && !partner.Downed;

			Pawn best = null;
			var bestOp = int.MinValue;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == champion || c.Dead || c.Downed) continue;
				if (partnerValid && c == partner) continue;
				if (c.ageTracker != null && c.ageTracker.AgeBiologicalYears < 16) continue;
				var op = champion.relations?.OpinionOf(c) ?? 0;
				if (op < CrownDislikeFloor) continue;
				if (op > bestOp) { bestOp = op; best = c; }
			}

			if (partnerValid)
			{
				var partnerOp = champion.relations?.OpinionOf(partner) ?? 0;
				if (best != null && bestOp > partnerOp + 5)
				{
					var chance = Mathf.Clamp((bestOp - partnerOp) / 120f, 0.15f, 0.7f);
					if (Rand.Chance(chance)) { jilted = partner; scandal = true; return best; }
				}
				return partner;
			}
			return best;
		}

		private static void Step(System.Action step)
		{
			try { step(); }
			catch (System.Exception e) { Log.Error("[RimJoust] nettoyage de fin de tournoi : " + e); }
		}

		private void Abort(string reason)
		{
			_active = false;
			Step(RefundBet);
			Step(ReleaseAllParticipants);
			Step(ReleaseSquires);
			Step(ReleaseHorses);
			Step(RestoreCanRideAll);
			Step(ReleaseGuests);
			InvalidateFieldCache();
			_boutA = _boutB = null;
			_clearingLoser = null;
			_oathQueue.Clear();
			_oathKnight = null;
			_remaining.Clear();
			_winners.Clear();
			_eliminated.Clear();
			_ceremonyTicks = 0;
			_ceremonyWaitTicks = 0;
			_lordArrived = false;
			_betweenBoutTicks = 0;
			_clearTicks = 0;
			_lastTournamentTick = -1;
			Messages.Message(reason, MessageTypeDefOf.NegativeEvent, historical: false);
		}

		private bool TryDispatchSquire(Pawn knight, Map map)
		{
			if (knight == null || map == null) return false;
			var spot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_RecoverySpot).FirstOrDefault();
			if (spot == null) return false;

			var squire = FindAvailableSquire(map, knight);
			if (squire == null) return false;

			if (squire.drafter != null) squire.drafter.Drafted = false;

			Job job;
			var bed = FindRecoveryBed(map, spot.Position, knight, squire);
			if (bed != null)
			{
				job = JobMaker.MakeJob(JobDefOf.Rescue, knight, bed);
				job.count = 1;
			}
			else
			{
				job = JobMaker.MakeJob(RimJoustDefOf.RimJoust_CarryToRecovery, knight, spot.Position);
				job.count = 1;
			}
			squire.jobs.StartJob(job, JobCondition.InterruptForced);
			return true;
		}

		private Pawn FindAvailableSquire(Map map, Pawn exclude)
		{
			Pawn best = null;
			var bestDist = float.MaxValue;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == exclude || !IsSquire(c)) continue;
				if (c.Dead || c.Downed) continue;
				if (IsParticipant(c)) continue;
				if (c.CurJobDef == RimJoustDefOf.RimJoust_CarryToRecovery || c.CurJobDef == JobDefOf.Rescue) continue;
				if (!c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				if (!c.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) continue;
				var d = c.Position.DistanceToSquared(exclude.Position);
				if (d < bestDist) { bestDist = d; best = c; }
			}
			return best;
		}

		private static Building_Bed FindRecoveryBed(Map map, IntVec3 recoveryPos, Pawn knight, Pawn squire)
		{
			const float maxRadiusSq = 18f * 18f;
			var deadSpot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_DeadSpot).FirstOrDefault();
			Building_Bed bestMedical = null, bestAny = null;
			float dM = float.MaxValue, dA = float.MaxValue;
			foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Bed))
			{
				if (!(t is Building_Bed bed)) continue;
				if (bed.ForPrisoners) continue;
				if (bed.IsForbidden(squire)) continue;
				if (!bed.AnyUnoccupiedSleepingSlot) continue;
				float dist = (bed.Position - recoveryPos).LengthHorizontalSquared;
				if (dist > maxRadiusSq) continue;
				if (deadSpot != null && (bed.Position - deadSpot.Position).LengthHorizontalSquared < dist)
					continue;
				if (!squire.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Deadly)) continue;
				if (bed.Medical) { if (dist < dM) { dM = dist; bestMedical = bed; } }
				else if (dist < dA) { dA = dist; bestAny = bed; }
			}
			return bestMedical ?? bestAny;
		}

		private void MoveLoserToRecovery(Pawn loser)
		{
			if (loser == null || !loser.Spawned) return;
			if (!TryGetField(out var a, out var b, out var perp, out _, out var map) || loser.Map != map) return;
			MoveAside(loser, RecoveryOrSidelineCell(map, perp, Midpoint(a, b)), map);
		}

		private int _fieldTick = -1;
		private bool _fieldOk;
		private IntVec3 _fSpotA, _fSpotB, _fPerp, _fAxis;
		private Map _fMap;

		private bool TryGetField(out IntVec3 spotA, out IntVec3 spotB, out IntVec3 perp, out IntVec3 axis, out Map map)
		{
			var now = Find.TickManager?.TicksGame ?? -1;
			if (now >= 0 && now == _fieldTick)
			{
				spotA = _fSpotA; spotB = _fSpotB; perp = _fPerp; axis = _fAxis; map = _fMap;
				return _fieldOk;
			}

			var ok = ComputeField(out spotA, out spotB, out perp, out axis, out map);
			_fieldTick = now; _fieldOk = ok;
			_fSpotA = spotA; _fSpotB = spotB; _fPerp = perp; _fAxis = axis; _fMap = map;
			return ok;
		}

		private bool ComputeField(out IntVec3 spotA, out IntVec3 spotB, out IntVec3 perp, out IntVec3 axis, out Map map)
		{
			spotA = spotB = IntVec3.Invalid; perp = axis = IntVec3.Zero; map = null;

			var anyPawn = _boutA ?? _boutB ?? _clearingLoser ?? _remaining.FirstOrDefault() ?? _winners.FirstOrDefault();
			map = anyPawn?.MapHeld;
			if (map == null) return false;

			var fa = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).FirstOrDefault();
			var fb = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotB).FirstOrDefault();
			if (fa == null || fb == null) return false;

			spotA = fa.Position; spotB = fb.Position;
			var dx = spotB.x - spotA.x; var dz = spotB.z - spotA.z;
			if (Mathf.Abs(dx) >= Mathf.Abs(dz)) { perp = new IntVec3(0, 0, 1); axis = new IntVec3(1, 0, 0); }
			else { perp = new IntVec3(1, 0, 0); axis = new IntVec3(0, 0, 1); }
			return true;
		}

		private void InvalidateFieldCache()
		{
			_fieldTick = -1;
			_fMap = null;
		}

		private int _cdTicksLeft = -1;
		private int _cdMapId = -1;
		private int _cdReportedTick = -1000;

		public void ReportStartCountdown(int ticksLeft, Map map)
		{
			_cdTicksLeft = ticksLeft;
			_cdMapId = map?.uniqueID ?? -1;
			_cdReportedTick = Find.TickManager?.TicksGame ?? 0;
		}

		public bool TryGetStartCountdown(Map map, out int secondsLeft, out float phase)
		{
			secondsLeft = 0; phase = 0f;
			if (_cdTicksLeft <= 0 || map == null || map.uniqueID != _cdMapId) return false;
			var now = Find.TickManager?.TicksGame ?? 0;
			if (now - _cdReportedTick > 2) return false;
			secondsLeft = Mathf.CeilToInt(_cdTicksLeft / 60f);
			phase = ((_cdTicksLeft - 1) % 60) / 59f;
			return true;
		}

		private const int GoFlashTicks = 45;
		private int _goFlashTick = -1000;

		public void FlashStartGo(Map map)
		{
			_cdMapId = map?.uniqueID ?? -1;
			_cdTicksLeft = -1;
			_goFlashTick = Find.TickManager?.TicksGame ?? 0;
			_boutCharging = true;
		}

		public bool TryGetStartGo(Map map, out float fade)
		{
			fade = 0f;
			if (map == null || map.uniqueID != _cdMapId) return false;
			var now = Find.TickManager?.TicksGame ?? 0;
			var elapsed = now - _goFlashTick;
			if (elapsed < 0 || elapsed > GoFlashTicks) return false;
			fade = 1f - (float)elapsed / GoFlashTicks;
			return true;
		}

		private static IntVec3 Midpoint(IntVec3 a, IntVec3 b)
			=> new IntVec3((a.x + b.x) / 2, a.y, (a.z + b.z) / 2);

		private IntVec3 RecoveryOrSidelineCell(Map map, IntVec3 perp, IntVec3 mid)
		{
			var spot = map?.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_RecoverySpot).FirstOrDefault();
			return spot != null ? spot.Position : mid - perp * StagingPerpOffset;
		}

		private void StagePawns()
		{
			MusterWaitingKnights();
			MusterSquires();
		}

		private void MusterSquires()
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var posts = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SquirePost);
			var postBase = Midpoint(a, b) - perp * SquirePostOffset;

			var squires = map.mapPawns.FreeColonistsSpawned
				.Where(c => IsSquire(c) && !c.Dead && !c.Downed && !IsParticipant(c)
					&& c.CurJobDef != RimJoustDefOf.RimJoust_CarryToRecovery
					&& c.CurJobDef != JobDefOf.Rescue
					&& c.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
				.ToList();

			for (var i = 0; i < squires.Count; i++)
			{
				var s = squires[i];
				if (SquireIsPosted(s, map)) continue;
				var spot = posts.Count > 0
					? posts[i % posts.Count].Position
					: postBase + axis * Mathf.RoundToInt((i - (squires.Count - 1) / 2f) * StagingSpacing);
				var cell = CellFinder.RandomClosewalkCellNear(spot, map, 1);
				if (s.drafter != null) s.drafter.Drafted = true;
				s.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
			}
		}

		private void ReleaseSquires()
		{
			foreach (var m in Find.Maps)
			{
				var list = m.mapPawns.FreeColonistsSpawned;
				for (var i = 0; i < list.Count; i++)
				{
					var c = list[i];
					if (IsSquire(c) && c.drafter != null && !c.Downed)
						c.drafter.Drafted = false;
				}
			}
		}

		public bool SquiresReady(Map map)
		{
			if (!_active || map == null) return true;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (!IsSquire(c) || c.Dead || c.Downed || IsParticipant(c)) continue;
				if (!c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				if (c.CurJobDef == RimJoustDefOf.RimJoust_CarryToRecovery
					|| c.CurJobDef == JobDefOf.Rescue) return false;
				if (c.pather != null && c.pather.Moving) return false;
				if (!SquireIsPosted(c, map)) return false;
			}
			return true;
		}

		private bool SquireIsPosted(Pawn s, Map map)
		{
			var posts = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SquirePost);
			if (posts.Count > 0)
			{
				for (var i = 0; i < posts.Count; i++)
					if (s.Position.DistanceTo(posts[i].Position) <= 3f) return true;
				return false;
			}
			if (!TryGetField(out var a, out var b, out var perp, out _, out _)) return true;
			var postBase = Midpoint(a, b) - perp * SquirePostOffset;
			return s.Position.DistanceTo(postBase) <= SquirePostOffset + 4f;
		}

		private void HorseHandlingTick()
		{
			if (++_horseTicks < HorseDispatchInterval) return;
			_horseTicks = 0;
			if (!TryGetField(out _, out _, out _, out _, out var map)) return;

			HandleLooseHorses(map);
			MusterSquires();
			MusterWaitingKnights();
			TidyFallenGuests(map);
		}

		private void HandleLooseHorses(Map map)
		{
			if (_looseHorses.Count == 0) return;

			_looseHorses.RemoveAll(h => h == null || h.Dead || !h.Spawned);

			var spot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_HorseSpot).FirstOrDefault();
			if (spot == null) return;

			foreach (var horse in _looseHorses)
			{
				if (horse.Position.DistanceTo(spot.Position) <= HorseParkRadius)
				{
					KeepHorseParked(horse);
					continue;
				}

				EnsureWalkingToSpot(horse, spot.Position, map);
			}
		}

		private void EnsureWalkingToSpot(Pawn horse, IntVec3 spotPos, Map map)
		{
			var target = NearestReachableCell(horse, spotPos, map);
			if (target.IsValid)
			{
				var cur = horse.CurJob;
				if (cur != null && cur.def == JobDefOf.Goto && cur.targetA.Cell.IsValid
					&& cur.targetA.Cell.DistanceTo(target) <= 3f)
					return;
				horse.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, target), JobCondition.InterruptForced);
				DbgHorse(horse, $"marche seul → case accessible à {target.DistanceTo(spotPos):F0} du point");
				return;
			}

			DbgHorse(horse, "aucune case accessible près du point → téléport");
			var dest = CellFinder.RandomClosewalkCellNear(spotPos, map, 3);
			horse.pather?.StopDead();
			horse.Position = dest;
			horse.Notify_Teleported(true, true);
		}

		private void KeepHorseParked(Pawn horse)
		{
			var cur = horse.CurJob;
			if (cur != null && cur.def == JobDefOf.Wait) return;
			var wait = JobMaker.MakeJob(JobDefOf.Wait);
			wait.expiryInterval = HorseParkWaitTicks;
			horse.jobs?.StartJob(wait, JobCondition.InterruptForced);
			DbgHorse(horse, "parqué → figé sur place (Wait)");
		}

		private static IntVec3 NearestReachableCell(Pawn horse, IntVec3 spotPos, Map map)
		{
			var count = GenRadial.NumCellsInRadius(HorseReachSearchRadius);
			for (var i = 0; i < count; i++)
			{
				var c = spotPos + GenRadial.RadialPattern[i];
				if (!c.InBounds(map) || !c.Standable(map)) continue;
				if (!horse.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
				return c;
			}
			return IntVec3.Invalid;
		}

		private void ReleaseHorses()
		{
			foreach (var h in _looseHorses)
				if (h?.roping != null && h.roping.IsRoped) h.roping.BreakAllRopes();
			_looseHorses.Clear();
		}

		private void ReleaseGuests()
		{
			foreach (var lo in _guestLoadouts)
			{
				try { GuestKnightUtility.ReleaseGuest(lo); }
				catch (System.Exception e) { Log.Error("[RimJoust] libération de l'invité " + (lo?.guest?.LabelShort ?? "?") + " : " + e); }
			}
			_guestLoadouts.Clear();
			_enrolledGuestIds.Clear();
			RestoreKnightWeapons();
		}

		private void RestoreKnightWeapons()
		{
			foreach (var lo in _knightLoadouts)
			{
				try { GuestKnightUtility.RestoreLance(lo); }
				catch (System.Exception e) { Log.Error("[RimJoust] restitution de l'arme de " + (lo?.knight?.LabelShort ?? "?") + " : " + e); }
			}
			_knightLoadouts.Clear();
		}

		private void RestoreCanRideAll()
		{
			for (var i = 0; i < _eliminated.Count; i++) RestoreCanRide(_eliminated[i]);
			for (var i = 0; i < _guestLoadouts.Count; i++) RestoreCanRide(_guestLoadouts[i]?.guest);
		}

		private static void RestoreCanRide(Pawn p)
		{
			if (p == null) return;
			var storage = ExtendedDataStorage.Singleton;
			if (storage == null) return;
			var data = storage.GetExtendedPawnData(p);
			if (data != null) data.canRide = true;
		}

		private Pawn _betFighter;
		private int _betStake;
		private float _betOdds;

		public bool HasActiveBet => _betFighter != null;
		public Pawn BetFighter => _betFighter;
		public int BetStake => _betStake;
		public float BetOdds => _betOdds;

		public bool HasGuests => _guestLoadouts.Count > 0;
		public bool CurrentBoutHasGuest
			=> _boutA != null && _boutB != null && (IsGuestParticipant(_boutA) || IsGuestParticipant(_boutB));
		public bool BetsClosed => _boutCharging;

		public int ColonySilverAvailable()
		{
			var map = _boutA?.MapHeld ?? _boutB?.MapHeld;
			return map != null ? ColonySilver(map) : 0;
		}

		private static int JoustLevelOf(Pawn p) => JoustSkill.LevelOf(p);

		private float FighterStrength(Pawn p)
		{
			if (p == null) return 1f;
			var s = 1f + JoustLevelOf(p) * 0.6f + WinsOf(p) * 0.8f;
			if (p.HasJoustingShield()) s += 1.2f;
			s += JoustTraitEffects.StrengthBonus(p);
			return Mathf.Max(0.3f, s);
		}

		public void GetOdds(out float oddsA, out float oddsB)
		{
			var sa = FighterStrength(_boutA);
			var sb = FighterStrength(_boutB);
			var pA = sa / (sa + sb);
			oddsA = Mathf.Clamp(0.9f / pA, 1.1f, 6f);
			oddsB = Mathf.Clamp(0.9f / (1f - pA), 1.1f, 6f);
		}

		public bool TryPlaceBet(Pawn on, int stake)
		{
			if (_betFighter != null) { Reject("RimJoust.BetAlready".Translate()); return false; }
			if (_boutA == null || _boutB == null) { Reject("RimJoust.BetNoBout".Translate()); return false; }
			if (!CurrentBoutHasGuest) { Reject("RimJoust.BetNoGuest".Translate()); return false; }
			if (_boutCharging) { Reject("RimJoust.BetClosed".Translate()); return false; }
			if (on != _boutA && on != _boutB || stake <= 0) return false;
			var map = _boutA.MapHeld ?? _boutB.MapHeld;
			if (map == null) return false;
			if (!TryConsumeSilver(map, stake)) { Reject("RimJoust.BetNeedSilver".Translate(stake)); return false; }

			GetOdds(out var oa, out var ob);
			_betFighter = on;
			_betStake = stake;
			_betOdds = on == _boutA ? oa : ob;
			Messages.Message("RimJoust.BetPlaced".Translate(stake, on.LabelShort, _betOdds.ToString("0.0")),
				new LookTargets(on), MessageTypeDefOf.NeutralEvent, historical: false);
			return true;
		}

		private void ResolveBet(Pawn winner)
		{
			if (_betFighter == null) return;
			var map = _betFighter.MapHeld ?? Find.AnyPlayerHomeMap ?? Find.CurrentMap;
			if (winner == _betFighter && map != null)
			{
				var cell = _betFighter.Spawned ? _betFighter.Position : map.Center;
				var payout = Mathf.RoundToInt(_betStake * _betOdds);
				PaySilver(map, payout, cell);
				Messages.Message("RimJoust.BetWon".Translate(_betFighter.LabelShort, payout),
					MessageTypeDefOf.PositiveEvent, historical: false);
			}
			else
			{
				Messages.Message("RimJoust.BetLost".Translate(_betStake), MessageTypeDefOf.NegativeEvent, historical: false);
			}
			_betFighter = null; _betStake = 0; _betOdds = 0f;
		}

		private void RefundBet()
		{
			if (_betFighter == null) return;
			var map = _betFighter.MapHeld ?? Find.AnyPlayerHomeMap ?? Find.CurrentMap;
			if (map != null) PaySilver(map, _betStake, _betFighter.Spawned ? _betFighter.Position : map.Center);
			_betFighter = null; _betStake = 0; _betOdds = 0f;
		}

		private static int ColonySilver(Map map)
		{
			var list = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
			var n = 0;
			for (var i = 0; i < list.Count; i++)
				if (!list[i].IsForbidden(Faction.OfPlayer)) n += list[i].stackCount;
			return n;
		}

		private static bool TryConsumeSilver(Map map, int amount)
		{
			if (ColonySilver(map) < amount) return false;
			var remaining = amount;
			foreach (var t in new List<Thing>(map.listerThings.ThingsOfDef(ThingDefOf.Silver)))
			{
				if (t.IsForbidden(Faction.OfPlayer)) continue;
				var take = Mathf.Min(t.stackCount, remaining);
				t.stackCount -= take;
				remaining -= take;
				if (t.stackCount <= 0) t.Destroy();
				if (remaining <= 0) break;
			}
			return remaining <= 0;
		}

		private static void PaySilver(Map map, int amount, IntVec3 near)
		{
			if (amount <= 0) return;
			var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
			silver.stackCount = amount;
			GenPlace.TryPlaceThing(silver, near.IsValid ? near : map.Center, map, ThingPlaceMode.Near);
		}

		public bool IsGuestParticipant(Pawn p)
		{
			if (p == null) return false;
			for (var i = 0; i < _guestLoadouts.Count; i++)
				if (_guestLoadouts[i]?.guest == p) return true;
			return false;
		}

		private const float GuestTidyRadius = 4f;
		private const float DeadBedSearchRadius = 15f;

		private void TidyFallenGuests(Map map)
		{
			if (_guestLoadouts.Count == 0 && _eliminated.Count == 0) return;

			for (var g = 0; g < _guestLoadouts.Count; g++)
			{
				var corpse = _guestLoadouts[g]?.guest?.Corpse;
				if (corpse == null || !corpse.Spawned || corpse.Map != map) continue;

				var items = new List<Thing>();
				CollectLooseItems(corpse, map, items);
				for (var i = 0; i < items.Count; i++)
				{
					var th = items[i];
					if (th == null || !th.Spawned) continue;
					if (th.def != null && th.def.IsJoustingLance()) { th.Destroy(); continue; }
					DispatchTidyHaul(th, map);
				}
			}

			for (var i = 0; i < _eliminated.Count; i++)
			{
				var c = _eliminated[i]?.Corpse;
				if (c != null && c.Spawned && c.Map == map)
					TryCarryCorpseToDeadSpot(c, map);
			}
		}

		private static void CollectLooseItems(Corpse corpse, Map map, List<Thing> outThings)
		{
			var count = GenRadial.NumCellsInRadius(GuestTidyRadius);
			for (var i = 0; i < count; i++)
			{
				var c = corpse.Position + GenRadial.RadialPattern[i];
				if (!c.InBounds(map)) continue;
				var list = c.GetThingList(map);
				for (var j = 0; j < list.Count; j++)
				{
					var t = list[j];
					if (t == corpse || t is Corpse || outThings.Contains(t)) continue;
					if (t.def != null && (t.def.EverHaulable || t.def.IsJoustingLance()))
					{
						t.SetForbidden(false, false);
						outThings.Add(t);
					}
				}
			}
		}

		private void TryCarryCorpseToDeadSpot(Corpse corpse, Map map)
		{
			var spot = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_DeadSpot).FirstOrDefault();
			if (spot == null) return;
			var recovery = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_RecoverySpot).FirstOrDefault();

			if (CorpseIsAtMorgue(corpse, spot.Position, recovery, map)) return;

			var squire = FindFreeSquireFor(map, corpse);
			if (squire == null) return;

			var dest = FindDeadDestination(map, spot.Position, squire);
			if (!dest.IsValid) return;

			corpse.SetForbidden(false, false);
			var job = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, dest);
			job.count = 1;
			job.haulMode = HaulMode.ToCellNonStorage;
			squire.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		private static bool CorpseIsAtMorgue(Corpse corpse, IntVec3 deadSpotPos, Thing recovery, Map map)
		{
			if (corpse.Position.GetEdifice(map) is Building_Bed bed)
			{
				var dDead = (bed.Position - deadSpotPos).LengthHorizontalSquared;
				return recovery == null
					|| dDead <= (bed.Position - recovery.Position).LengthHorizontalSquared;
			}
			return corpse.Position.DistanceTo(deadSpotPos) <= 3f;
		}

		private IntVec3 FindDeadDestination(Map map, IntVec3 spotPos, Pawn squire)
		{
			var maxSq = DeadBedSearchRadius * DeadBedSearchRadius;
			var recovery = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_RecoverySpot).FirstOrDefault();
			var best = IntVec3.Invalid;
			var bestD = float.MaxValue;
			foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Bed))
			{
				if (!(t is Building_Bed bed) || bed.ForPrisoners) continue;
				var dDead = (bed.Position - spotPos).LengthHorizontalSquared;
				if (dDead > maxSq) continue;
				if (recovery != null && (bed.Position - recovery.Position).LengthHorizontalSquared < dDead)
					continue;
				foreach (var c in bed.OccupiedRect())
				{
					if (CellHasCorpse(c, map)) continue;
					if (!squire.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
					var d = (c - spotPos).LengthHorizontalSquared;
					if (d < bestD) { bestD = d; best = c; }
				}
			}
			if (best.IsValid) return best;

			if (spotPos.Standable(map) && !CellHasCorpse(spotPos, map)
				&& squire.CanReserveAndReach(spotPos, PathEndMode.OnCell, Danger.Deadly))
				return spotPos;
			var count = GenRadial.NumCellsInRadius(4f);
			for (var i = 1; i < count; i++)
			{
				var c = spotPos + GenRadial.RadialPattern[i];
				if (!c.InBounds(map) || !c.Standable(map) || CellHasCorpse(c, map)) continue;
				if (squire.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly)) return c;
			}
			return IntVec3.Invalid;
		}

		private static bool CellHasCorpse(IntVec3 c, Map map)
		{
			if (!c.InBounds(map)) return false;
			var list = c.GetThingList(map);
			for (var i = 0; i < list.Count; i++) if (list[i] is Corpse) return true;
			return false;
		}

		private void DispatchTidyHaul(Thing th, Map map)
		{
			var squire = FindFreeSquireFor(map, th);
			if (squire == null) return;
			var job = HaulAIUtility.HaulToStorageJob(squire, th, forced: false);
			if (job == null) return;
			squire.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		private Pawn FindFreeSquireFor(Map map, Thing th)
		{
			var list = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < list.Count; i++)
			{
				var c = list[i];
				if (!IsSquire(c) || c.Dead || c.Downed) continue;
				if (c.carryTracker?.CarriedThing != null) continue;
				if (c.CurJobDef == JobDefOf.HaulToCell || c.CurJobDef == JobDefOf.HaulToContainer) continue;
				if (!c.CanReserveAndReach(th, PathEndMode.ClosestTouch, Danger.Deadly)) continue;
				return c;
			}
			return null;
		}

		private void ShooBystandersOffLane()
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.z - a.z));

			for (var t = -LaneClearEndMargin; t <= steps + LaneClearEndMargin; t++)
			{
				var baseCell = a + axis * t;
				for (var s = -LaneClearHalfWidth; s <= LaneClearHalfWidth; s++)
				{
					var cell = baseCell + perp * s;
					if (!cell.InBounds(map)) continue;
					var things = map.thingGrid.ThingsListAtFast(cell);
					for (var i = 0; i < things.Count; i++)
						if (things[i] is Pawn p && ShouldShoo(p))
							ShooOff(p, cell, perp, s, map);
				}
			}
		}

		private bool ShouldShoo(Pawn p)
		{
			if (p == null || !p.Spawned || p.Dead || p.Downed) return false;
			if (p.Drafted) return false;
			if (p.pather != null && p.pather.Moving) return false;
			if (IsParticipant(p) || IsSquire(p)) return false;
			if (p.CurJobDef == RimJoustDefOf.RimJoust_WatchJoust) return false;
			if (p.IsMountedAnimal(out _)) return false;
			if (_looseHorses.Contains(p)) return false;
			return true;
		}

		private void ShooOff(Pawn p, IntVec3 cell, IntVec3 perp, int s, Map map)
		{
			var side = s >= 0 ? 1 : -1;
			var target = cell + perp * (side * (LaneClearHalfWidth + LaneClearPushOut));
			if (!target.InBounds(map) || !target.Standable(map)
				|| !p.CanReach(target, PathEndMode.OnCell, Danger.Deadly))
			{
				if (!CellFinder.TryFindRandomCellNear(target, map, 4,
						c => c.InBounds(map) && c.Standable(map)
							&& p.CanReach(c, PathEndMode.OnCell, Danger.Deadly),
						out target))
					return;
			}
			p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, target), JobCondition.InterruptForced);
		}

		private static void DeleteLegacyJoustZoneAreas()
		{
			foreach (var map in Find.Maps)
			{
				var area = map.areaManager.AllAreas.OfType<Area_Allowed>()
					.FirstOrDefault(ar => ar.Label == LegacyJoustZoneLabel);
				area?.Delete();
			}
		}

		private void GatherSpectators()
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var mid = Midpoint(a, b);
			var steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.z - a.z));

			var cells = new List<IntVec3>();
			for (var t = -SpectatorEndMargin; t <= steps + SpectatorEndMargin; t++)
				for (var side = -1; side <= 1; side += 2)
					for (var d = SpectatorInnerOffset; d <= SpectatorInnerOffset + SpectatorDepth; d++)
					{
						var cell = a + axis * t + perp * (side * d);
						if (cell.InBounds(map) && cell.Standable(map)) cells.Add(cell);
					}

			GatherCrowd(map, mid, mid, cells, null, 1);
		}

		public void GatherCrowd(Map map, IntVec3 gaze, IntVec3 chairAnchor, List<IntVec3> standingCells,
			System.Func<IntVec3, bool> chairFilter = null, int eventKind = 0)
		{
			if (map == null) return;
			var targets = new List<LocalTargetInfo>();

			var chairCells = new List<IntVec3>();
			var allBld = map.listerBuildings.allBuildingsColonist;
			for (var i = 0; i < allBld.Count; i++)
			{
				var bld = allBld[i];
				if (bld?.def?.building == null || !bld.def.building.isSittable) continue;
				if (bld.Position.DistanceTo(chairAnchor) > SpectatorChairRadius) continue;
				foreach (var cell in bld.OccupiedRect())
					if (cell.InBounds(map) && cell.Standable(map) && (chairFilter == null || chairFilter(cell)))
						chairCells.Add(cell);
			}
			chairCells.Sort((x, y) => x.DistanceToSquared(chairAnchor).CompareTo(y.DistanceToSquared(chairAnchor)));
			for (var i = 0; i < chairCells.Count; i++) targets.Add(chairCells[i]);

			if (standingCells != null)
			{
				standingCells.Sort((x, y) => x.DistanceToSquared(chairAnchor).CompareTo(y.DistanceToSquared(chairAnchor)));
				for (var i = 0; i < standingCells.Count; i++) targets.Add(standingCells[i]);
			}

			if (targets.Count == 0) return;

			var ti = 0;
			foreach (var c in map.mapPawns.FreeColonistsSpawned.ToList())
			{
				if (!IsEligibleSpectator(c)) continue;
				if (c.CurJobDef == RimJoustDefOf.RimJoust_WatchJoust) continue;

				var target = LocalTargetInfo.Invalid;
				while (ti < targets.Count)
				{
					var cand = targets[ti++];
					if (c.CanReserveAndReach(cand, PathEndMode.OnCell, Danger.Deadly)) { target = cand; break; }
				}
				if (!target.IsValid) break;

				var job = JobMaker.MakeJob(RimJoustDefOf.RimJoust_WatchJoust, target, gaze);
				job.count = eventKind;
				c.jobs.StartJob(job, JobCondition.InterruptForced);
			}
		}

		private bool IsEligibleSpectator(Pawn c)
		{
			if (c == null || !c.Spawned || c.Dead || c.Downed) return false;
			if (c.Drafted || c.InMentalState) return false;
			if (IsParticipant(c) || IsSquire(c)) return false;
			if (_melee != null && _melee.Covers(c)) return false;
			if (_archery != null && _archery.IsArcher(c)) return false;
			if (c.health == null || !c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			if (c.CurJobDef == JobDefOf.LayDown) return false;
			if (c.CurJobDef == JobDefOf.TendPatient) return false;
			var food = c.needs?.food;
			var rest = c.needs?.rest;
			if (food != null && food.CurLevelPercentage < 0.12f) return false;
			if (rest != null && rest.CurLevelPercentage < 0.12f) return false;
			return true;
		}

		private void MusterWaitingKnights()
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var rowBase = Midpoint(a, b) + perp * StagingPerpOffset;

			var posts = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_KnightPost);

			var knights = WaitingKnights().Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList();
			for (var i = 0; i < knights.Count; i++)
			{
				var p = knights[i];
				IntVec3 cell;
				if (posts.Count > 0)
					cell = posts[i % posts.Count].Position;
				else
				{
					var spot = rowBase + axis * Mathf.RoundToInt((i - (knights.Count - 1) / 2f) * StagingSpacing);
					cell = CellFinder.RandomClosewalkCellNear(spot, map, 1);
				}
				if (p.drafter != null) p.drafter.Drafted = true;

				var atPost = p.Position.DistanceTo(cell) <= 3f;
				if (!atPost)
				{
					if (p.CurJobDef != JobDefOf.Goto)
						p.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
				}
				else if (IsGuestParticipant(p))
				{
					if (p.CurJobDef != JobDefOf.Wait_Combat)
					{
						var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
						wait.expiryInterval = 5000;
						p.jobs?.StartJob(wait, JobCondition.InterruptForced);
					}
				}
			}
		}

		private void EnsureGuestMounted(Pawn p)
		{
			if (p == null) return;
			for (var i = 0; i < _guestLoadouts.Count; i++)
				if (_guestLoadouts[i]?.guest == p)
				{
					GuestKnightUtility.RemountGuest(_guestLoadouts[i], p.Map);
					return;
				}
		}

		private IEnumerable<Pawn> WaitingKnights()
		{
			foreach (var p in _remaining) if (p != _boutA && p != _boutB) yield return p;
			foreach (var p in _winners) if (p != _boutA && p != _boutB) yield return p;
		}

		private static void MoveAside(Pawn p, IntVec3 baseCell, Map map)
		{
			if (p == null || !p.Spawned || p.Map != map) return;
			var cell = CellFinder.RandomClosewalkCellNear(baseCell, map, 4);
			p.pather?.StopDead();
			p.Position = cell;
			p.Notify_Teleported(true, true);
		}

		private void SweepLaneDebris()
		{
			if (!TryGetField(out var a, out var b, out var perp, out var axis, out var map)) return;
			var dump = RecoveryOrSidelineCell(map, perp, Midpoint(a, b));
			var steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.z - a.z));

			var toMove = new List<Thing>();
			for (var t = -1; t <= steps + 1; t++)
			{
				var baseCell = a + axis * t;
				for (var s = -2; s <= 2; s++)
				{
					var c = baseCell + perp * s;
					if (!c.InBounds(map)) continue;
					var things = map.thingGrid.ThingsListAtFast(c);
					for (var i = 0; i < things.Count; i++)
					{
						var th = things[i];
						if (th is Corpse) toMove.Add(th);
						else if (th.def != null && th.def.IsJoustingLance()) toMove.Add(th);
					}
				}
			}
			foreach (var th in toMove) MoveThingAside(th, dump, map);
		}

		private static void MoveThingAside(Thing th, IntVec3 baseCell, Map map)
		{
			if (th == null || !th.Spawned || th.Map != map) return;
			var cell = CellFinder.RandomClosewalkCellNear(baseCell, map, 4);
			th.DeSpawn();
			GenPlace.TryPlaceThing(th, cell, map, ThingPlaceMode.Near);
		}

		private void ReleaseAllParticipants()
		{
			foreach (var p in AllTracked())
				if (p?.drafter != null && p.Spawned && !p.Downed)
					p.drafter.Drafted = false;
		}

		private IEnumerable<Pawn> AllTracked()
		{
			if (_boutA != null) yield return _boutA;
			if (_boutB != null) yield return _boutB;
			foreach (var p in _remaining) yield return p;
			foreach (var p in _winners) yield return p;
		}

		private static void Reject(string msg)
			=> Messages.Message(msg, MessageTypeDefOf.RejectInput, historical: false);

		private const int MaxLogEntries = 300;

		private void LogEvent(string text)
		{
			if (string.IsNullOrEmpty(text)) return;
			_log.Add(text);
			if (_log.Count > MaxLogEntries) _log.RemoveRange(0, _log.Count - MaxLogEntries);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _active, "active");
			Scribe_Values.Look(ref _shownIntro, "shownIntro");
			Scribe_Values.Look(ref _showRoleGizmos, "showRoleGizmos", true);
			Scribe_Values.Look(ref _allowGuestArrivals, "allowGuestArrivals", true);
			Scribe_Values.Look(ref _lastTournamentTick, "lastTournamentTick", -1);
			Scribe_Values.Look(ref _betweenBoutTicks, "betweenBoutTicks");
			Scribe_Values.Look(ref _boutGraceTicks, "boutGraceTicks");
			Scribe_Values.Look(ref _boutTicks, "boutTicks");
			Scribe_Values.Look(ref _clearTicks, "clearTicks");
			Scribe_Values.Look(ref _squireOnIt, "squireOnIt");
			Scribe_Values.Look(ref _roundNumber, "roundNumber");
			Scribe_Values.Look(ref _totalParticipants, "totalParticipants");
			Scribe_Collections.Look(ref _wins, "wins", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref _history, "history", LookMode.Deep);
			Scribe_Collections.Look(ref _log, "log", LookMode.Value);
			Scribe_Collections.Look(ref _pastTournaments, "pastTournaments", LookMode.Deep);
			Scribe_Deep.Look(ref _melee, "melee");
			Scribe_Deep.Look(ref _archery, "archery");
			Scribe_Deep.Look(ref _grand, "grand");
			Scribe_References.Look(ref _champion, "champion");
			Scribe_References.Look(ref _queen, "queen");
			Scribe_References.Look(ref _lord, "lord");
			Scribe_Values.Look(ref _ceremonyTicks, "ceremonyTicks");
			Scribe_Values.Look(ref _ceremonyWaitTicks, "ceremonyWaitTicks");
			Scribe_Values.Look(ref _lordArrived, "lordArrived");
			Scribe_Values.Look(ref _ceremonySeatCell, "ceremonySeatCell");
			Scribe_Collections.Look(ref _oathQueue, "oathQueue", LookMode.Reference);
			Scribe_References.Look(ref _oathKnight, "oathKnight");
			Scribe_Values.Look(ref _oathPhase, "oathPhase");
			Scribe_Values.Look(ref _oathTicks, "oathTicks");
			Scribe_Values.Look(ref _oathReturned, "oathReturned");
			Scribe_Values.Look(ref _oathTotal, "oathTotal");
			Scribe_Values.Look(ref _oathCell, "oathCell");
			Scribe_Collections.Look(ref _enrolledGuestIds, "enrolledGuestIds", LookMode.Value);
			Scribe_Collections.Look(ref _guestLoadouts, "guestLoadouts", LookMode.Deep);
			Scribe_Collections.Look(ref _knightLoadouts, "knightLoadouts", LookMode.Deep);
			Scribe_References.Look(ref _boutA, "boutA");
			Scribe_References.Look(ref _boutB, "boutB");
			Scribe_References.Look(ref _clearingLoser, "clearingLoser");
			Scribe_References.Look(ref _betFighter, "betFighter");
			Scribe_Values.Look(ref _betStake, "betStake");
			Scribe_Values.Look(ref _betOdds, "betOdds");
			Scribe_Values.Look(ref _boutCharging, "boutCharging");
			Scribe_Collections.Look(ref _remaining, "remaining", LookMode.Reference);
			Scribe_Collections.Look(ref _winners, "winners", LookMode.Reference);
			Scribe_Collections.Look(ref _eliminated, "eliminated", LookMode.Reference);
			Scribe_Collections.Look(ref _squireIds, "squireIds", LookMode.Value);
			Scribe_Collections.Look(ref _jousterIds, "jousterIds", LookMode.Value);
			Scribe_Collections.Look(ref _looseHorses, "looseHorses", LookMode.Reference);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_remaining ??= new List<Pawn>();
				_winners ??= new List<Pawn>();
				_eliminated ??= new List<Pawn>();
				_wins ??= new Dictionary<int, int>();
				_history ??= new List<BoutRecord>();
				_log ??= new List<string>();
				_enrolledGuestIds ??= new HashSet<int>();
				_guestLoadouts ??= new List<GuestLoadout>();
				_knightLoadouts ??= new List<KnightLoadout>();
				_squireIds ??= new HashSet<int>();
				_jousterIds ??= new HashSet<int>();
				_looseHorses ??= new List<Pawn>();
				_oathQueue ??= new List<Pawn>();
				_pastTournaments ??= new List<TournamentRecord>();
				_melee ??= new MeleeBrawl();
				_archery ??= new ArcheryContest();
				_grand ??= new GrandTournament();
			}
		}
	}
}
