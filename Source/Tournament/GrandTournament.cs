using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimJoust.Tournament
{
	public class GrandTournament : IExposable
	{
		private const int PauseTicks = 3500;
		private const int MaxTickFailures = 300;

		private int _phase;
		private int _ticks;
		private List<Pawn> _roster = new List<Pawn>();
		private Dictionary<int, int> _points = new Dictionary<int, int>();
		private List<Pawn> _joustRank = new List<Pawn>();
		private int _tickFailures;

		public bool Active => _phase > 0;

		public int PointsOf(Pawn p)
			=> p != null && _points.TryGetValue(p.thingIDNumber, out var n) ? n : 0;

		public bool TryStart(TournamentManager tm, Map map, List<Pawn> fighters)
		{
			if (Active || tm == null || map == null) return false;
			var valid = fighters?
				.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed)
				.Distinct()
				.ToList();
			if (valid == null || valid.Count < 2) return false;

			_roster = valid;
			_points.Clear();
			_joustRank.Clear();
			_ticks = 0;
			_tickFailures = 0;

			if (!tm.TryStartTournament(valid))
			{
				_roster.Clear();
				return false;
			}

			_phase = 1;
			Messages.Message("RimJoust.GrandStarted".Translate(valid.Count),
				new LookTargets(valid), MessageTypeDefOf.PositiveEvent, historical: false);
			return true;
		}

		public void Tick(TournamentManager tm)
		{
			if (!Active) return;
			try
			{
				TickInner(tm);
				_tickFailures = 0;
			}
			catch (System.Exception e)
			{
				_tickFailures++;
				if (_tickFailures == 1)
					Log.Error("[RimJoust] erreur pendant le grand tournoi : " + e);
				if (_tickFailures >= MaxTickFailures)
				{
					Log.Error("[RimJoust] le grand tournoi plante en boucle, annulation.");
					try { Cancel(tm); }
					catch { _phase = 0; }
				}
			}
		}

		private void TickInner(TournamentManager tm)
		{
			_roster.RemoveAll(p => p == null);
			if (_roster.Count == 0) { CancelSilent(); return; }

			switch (_phase)
			{
				case 1:
					if (tm.Active) return;
					if (tm.Champion == null) { CancelSilent(); return; }
					_joustRank = HarvestJoustRanking(tm);
					AwardPoints(_joustRank);
					Messages.Message("RimJoust.GrandNextArchery".Translate(),
						MessageTypeDefOf.NeutralEvent, historical: false);
					_phase = 2;
					_ticks = 0;
					return;

				case 2:
					_ticks++;
					if (_ticks == 1 || _ticks % 250 == 0) MusterAtPosts();
					if (_ticks < PauseTicks || WaitingOnNeeds()) return;
					StartArchery(tm);
					return;

				case 3:
					if (tm.Archery.Active) return;
					AwardPoints(tm.Archery.LastRanking.ToList());
					Messages.Message("RimJoust.GrandNextMelee".Translate(),
						MessageTypeDefOf.NeutralEvent, historical: false);
					_phase = 4;
					_ticks = 0;
					return;

				case 4:
					_ticks++;
					if (_ticks == 1 || _ticks % 250 == 0) MusterAtPosts();
					if (_ticks < PauseTicks || WaitingOnNeeds()) return;
					StartMelee(tm);
					return;

				case 5:
					if (tm.Melee.Active) return;
					AwardPoints(tm.Melee.EliminationOrder.Reverse().ToList());
					_phase = 6;
					return;

				case 6:
					Wrap(tm);
					return;
			}
		}

		private List<Pawn> HarvestJoustRanking(TournamentManager tm)
		{
			var rank = new List<Pawn>();
			if (tm.Champion != null && _roster.Contains(tm.Champion)) rank.Add(tm.Champion);
			for (var i = tm.Eliminated.Count - 1; i >= 0; i--)
			{
				var p = tm.Eliminated[i];
				if (p != null && !rank.Contains(p) && _roster.Contains(p)) rank.Add(p);
			}
			return rank;
		}

		private void AwardPoints(List<Pawn> bestFirst)
		{
			if (bestFirst == null) return;
			var m = bestFirst.Count;
			for (var i = 0; i < m; i++)
			{
				var p = bestFirst[i];
				if (p == null || !_roster.Contains(p)) continue;
				_points.TryGetValue(p.thingIDNumber, out var cur);
				_points[p.thingIDNumber] = cur + (m - 1 - i);
			}
		}

		private List<Pawn> Eligible()
			=> _roster.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed).ToList();

		private Map MapOf()
			=> _roster.FirstOrDefault(p => p?.MapHeld != null)?.MapHeld ?? Find.AnyPlayerHomeMap;

		private const int MaxPauseTicks = 15000;

		private bool WaitingOnNeeds()
		{
			if (_ticks >= MaxPauseTicks) return false;
			foreach (var p in Eligible())
				if (NeedsBreak(p)) return true;
			return false;
		}

		private static bool NeedsBreak(Pawn p)
		{
			var food = p.needs?.food;
			var rest = p.needs?.rest;
			if (food != null && food.CurLevelPercentage < 0.32f) return true;
			if (rest != null && rest.CurLevelPercentage < 0.22f) return true;
			return false;
		}

		private void MusterAtPosts()
		{
			var map = MapOf();
			if (map == null) return;

			var posts = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_KnightPost);
			var anchor = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).FirstOrDefault();
			var eligible = Eligible();

			for (var i = 0; i < eligible.Count; i++)
			{
				var p = eligible[i];
				if (p.jobs == null || p.Map != map) continue;

				if (NeedsBreak(p))
				{
					if (p.drafter != null && p.Drafted) p.drafter.Drafted = false;
					continue;
				}

				IntVec3 cell;
				if (posts.Count > 0) cell = posts[i % posts.Count].Position;
				else if (anchor != null) cell = CellFinder.RandomClosewalkCellNear(anchor.Position, map, 4);
				else continue;

				if (p.drafter == null) p.drafter = new Pawn_DraftController(p);
				p.drafter.Drafted = true;

				if (p.Position.DistanceTo(cell) <= 2f) continue;
				if (p.CurJobDef == JobDefOf.Goto && p.pather.Moving) continue;
				try
				{
					p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
				}
				catch (System.Exception e)
				{
					Log.Warning("[RimJoust] " + p.LabelShort + " refuse de rejoindre son poste (" + e.Message + ").");
				}
			}
		}

		private void StartArchery(TournamentManager tm)
		{
			var map = MapOf();
			var eligible = Eligible();
			var target = map != null ? ArcheryContest.FindTarget(map) : null;
			var spot = map != null ? ArcheryContest.FindSpot(map) : null;
			if (map == null || target == null || spot == null || eligible.Count < 2
				|| !tm.Archery.TryStart(eligible, spot.Position, target, map))
			{
				Messages.Message("RimJoust.GrandSkipArchery".Translate(),
					MessageTypeDefOf.NegativeEvent, historical: false);
				_phase = 4;
				_ticks = PauseTicks - 300;
				return;
			}
			_phase = 3;
			_ticks = 0;
		}

		private void StartMelee(TournamentManager tm)
		{
			var map = MapOf();
			var eligible = Eligible();
			if (map == null || eligible.Count < 2
				|| !tm.Melee.TryStart(eligible, MeleeBrawl.FindCenter(map, eligible), map))
			{
				Messages.Message("RimJoust.GrandSkipMelee".Translate(),
					MessageTypeDefOf.NegativeEvent, historical: false);
				_phase = 6;
				return;
			}
			_phase = 5;
			_ticks = 0;
		}

		private void Wrap(TournamentManager tm)
		{
			var standings = _roster
				.Where(p => p != null)
				.OrderByDescending(PointsOf)
				.ThenBy(p => { var i = _joustRank.IndexOf(p); return i < 0 ? int.MaxValue : i; })
				.ToList();
			var champion = standings.FirstOrDefault();
			var points = new Dictionary<int, int>(_points);
			var total = _roster.Count;

			_phase = 0;
			_roster.Clear();
			_points.Clear();
			_joustRank.Clear();

			tm.FinishGrandTournament(champion, standings, points, total);
		}

		public void Cancel(TournamentManager tm)
		{
			if (!Active) return;
			if (tm != null)
			{
				if (tm.Archery.Active) tm.Archery.Cancel();
				if (tm.Melee.Active) tm.Melee.Cancel();
				if (tm.Active) { _phase = 0; tm.CancelTournament(); }
			}
			CancelSilent();
		}

		private void CancelSilent()
		{
			if (_phase == 0 && _roster.Count == 0) return;
			_phase = 0;
			_roster.Clear();
			_points.Clear();
			_joustRank.Clear();
			Messages.Message("RimJoust.GrandCancelled".Translate(),
				MessageTypeDefOf.NegativeEvent, historical: false);
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref _phase, "phase");
			Scribe_Values.Look(ref _ticks, "ticks");
			Scribe_Collections.Look(ref _roster, "roster", LookMode.Reference);
			Scribe_Collections.Look(ref _points, "points", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref _joustRank, "joustRank", LookMode.Reference);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_roster ??= new List<Pawn>();
				_points ??= new Dictionary<int, int>();
				_joustRank ??= new List<Pawn>();
				_roster.RemoveAll(p => p == null);
			}
		}
	}
}
