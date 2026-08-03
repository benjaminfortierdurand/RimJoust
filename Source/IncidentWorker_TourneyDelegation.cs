using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimJoust
{
	public class IncidentWorker_TourneyDelegation : IncidentWorker
	{
		private const int MinKnights = 3;
		private const int MaxKnights = 5;

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!base.CanFireNowSub(parms)) return false;
			if (!(parms.target is Map map)) return false;

			var tm = RimJoust.Tournament.TournamentManager.Instance;
			if (tm == null || tm.Active || tm.TournamentOnCooldown || !tm.AllowGuestArrivals) return false;
			if (!RimJoust.Tournament.TournamentManager.ArenaReady(map)) return false;

			return KnightArrivalUtility.TryFindFaction(out _)
				&& RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Friendly);
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly)) return false;

			var count = Rand.RangeInclusive(MinKnights, MaxKnights);
			foreach (var faction in KnightArrivalUtility.EligibleFactionsInRandomOrder())
			{
				var knights = new List<Pawn>();
				for (var i = 0; i < count; i++)
				{
					var k = KnightArrivalUtility.GenerateKnight(faction, map.Tile);
					if (k == null) break;
					knights.Add(k);
				}
				if (knights.Count == 0) continue;

				foreach (var k in knights)
					GenSpawn.Spawn(k, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);

				if (!RCellFinder.TryFindRandomSpotJustOutsideColony(knights[0].Position, map, out var chillSpot))
					chillSpot = knights[0].Position;

				LordMaker.MakeNewLord(faction, new LordJob_VisitColony(faction, chillSpot), map, knights);

				SendStandardLetter(
					"RimJoust.DelegationArrivesLabel".Translate(),
					"RimJoust.DelegationArrivesText".Translate(faction.Name, knights.Count),
					LetterDefOf.PositiveEvent, parms, knights[0]);
				return true;
			}
			return false;
		}
	}
}
