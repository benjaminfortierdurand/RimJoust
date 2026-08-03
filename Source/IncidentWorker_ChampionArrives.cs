using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimJoust
{
	public class IncidentWorker_ChampionArrives : IncidentWorker
	{
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
			if (!(parms.target is Map map))
			{
				if (Prefs.DevMode) Log.Message("[RimJoust] champion : pas de carte cible (lance via 'Execute incident on map').");
				return false;
			}
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly))
			{
				if (Prefs.DevMode) Log.Message("[RimJoust] champion : pas de case d'entrée trouvée sur le bord de carte.");
				return false;
			}

			foreach (var faction in KnightArrivalUtility.EligibleFactionsInRandomOrder())
			{
				var champ = KnightArrivalUtility.GenerateKnight(faction, map.Tile);
				if (champ == null) continue;

				GenSpawn.Spawn(champ, entry, map);

				if (!RCellFinder.TryFindRandomSpotJustOutsideColony(champ.Position, map, out var chillSpot))
					chillSpot = champ.Position;

				LordMaker.MakeNewLord(faction, new LordJob_VisitColony(faction, chillSpot), map, new List<Pawn> { champ });

				SendStandardLetter(
					"RimJoust.ChampionArrivesLabel".Translate(),
					"RimJoust.ChampionArrivesText".Translate(champ.LabelShortCap, faction.Name),
					LetterDefOf.PositiveEvent, parms, champ);
				return true;
			}

			if (Prefs.DevMode) Log.Message("[RimJoust] champion : aucune faction n'a pu fournir de chevalier.");
			return false;
		}
	}
}
