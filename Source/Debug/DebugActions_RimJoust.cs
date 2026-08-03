using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimJoust.Debug
{
	public static class DebugActions_RimJoust
	{
		[DebugAction("RimJoust",
			"Faire venir une délégation (choisir la maison)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnDelegationFromFaction()
		{
			var map = Find.CurrentMap;
			if (map == null) return;

			var options = new List<DebugMenuOption>();
			foreach (var f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer || KnightArrivalUtility.GetHumanlikeKind(f) == null) continue;
				var faction = f;
				options.Add(new DebugMenuOption(faction.Name + " (" + faction.def.defName + ")",
					DebugMenuOptionMode.Action, () => SpawnDelegation(faction, map)));
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}

		private static void SpawnDelegation(Faction faction, Map map)
		{
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly))
				entry = map.Center;

			var knights = new List<Pawn>();
			for (var i = 0; i < 4; i++)
			{
				var k = KnightArrivalUtility.GenerateKnight(faction, map.Tile);
				if (k == null) continue;
				GenSpawn.Spawn(k, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
				knights.Add(k);
			}
			if (knights.Count == 0) { Log.Warning("[RimJoust] aucun chevalier généré pour " + faction.Name); return; }

			if (!RCellFinder.TryFindRandomSpotJustOutsideColony(knights[0].Position, map, out var chill))
				chill = knights[0].Position;
			Verse.AI.Group.LordMaker.MakeNewLord(faction,
				new LordJob_VisitColony(faction, chill), map, knights);
			Messages.Message("RimJoust : " + knights.Count + " chevaliers de " + faction.Name,
				new LookTargets(knights), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void WarnIfNotReady(Pawn pawn)
		{
			if (!pawn.HasJoustingLance())
				Log.Warning($"[RimJoust] {pawn.LabelShort} n'a pas de lance de joute équipée — le coup ne désarçonnera pas.");
			if (!GiddyUp.ExtendedDataStorage.isMounted.Contains(pawn.thingIDNumber))
				Log.Warning($"[RimJoust] {pawn.LabelShort} n'est pas monté — pas de désarçonnement possible.");
		}

		[DebugAction("RimJoust",
			"Lancer un TOURNOI (2+ pions sélectionnés)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartTournament()
		{
			var pawns = Find.Selector.SelectedObjects.OfType<Pawn>().ToList();
			if (pawns.Count < 2)
			{
				Messages.Message("RimJoust : sélectionne au moins 2 pions pour un tournoi.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			foreach (var p in pawns)
				WarnIfNotReady(p);

			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null)
			{
				Log.Error("[RimJoust] TournamentManager introuvable (GameComponent non chargé ?).");
				return;
			}
			manager.TryStartTournament(pawns);
		}

		[DebugAction("RimJoust",
			"Annuler le tournoi en cours",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void CancelTournament()
		{
			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null || !manager.Active)
			{
				Messages.Message("RimJoust : aucun tournoi en cours.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			manager.CancelTournament();
		}

		[DebugAction("RimJoust",
			"Lancer une MÊLÉE (2+ pions sélectionnés)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartMelee()
		{
			var pawns = Find.Selector.SelectedObjects.OfType<Pawn>()
				.Where(p => p.RaceProps.Humanlike).ToList();
			if (pawns.Count < 2)
			{
				Messages.Message("RimJoust : sélectionne au moins 2 pions humains.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null) return;
			if (manager.Active)
			{
				Messages.Message("RimJoust : un tournoi de joute est en cours, pas de mêlée en même temps.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var map = pawns[0].Map;
			var center = RimJoust.Tournament.MeleeBrawl.FindCenter(map, pawns);
			if (!RimJoust.Tournament.MeleeBrawl.HasMeleeGround(map))
				Messages.Message("RimJoust : pas de poteau de mêlée posé, la mêlée se fera à " + center + ".",
					MessageTypeDefOf.CautionInput, historical: false);

			if (!manager.Melee.TryStart(pawns, center, map))
				Messages.Message("RimJoust : impossible de lancer la mêlée.",
					MessageTypeDefOf.RejectInput, historical: false);
		}

		[DebugAction("RimJoust",
			"Annuler la mêlée",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void CancelMelee()
		{
			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null || !manager.Melee.Active)
			{
				Messages.Message("RimJoust : aucune mêlée en cours.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			manager.Melee.Cancel();
		}

		[DebugAction("RimJoust",
			"Lancer un CONCOURS DE TIR (2+ pions sélectionnés)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartArchery()
		{
			var pawns = Find.Selector.SelectedObjects.OfType<Pawn>()
				.Where(p => p.RaceProps.Humanlike).ToList();
			if (pawns.Count < 2)
			{
				Messages.Message("RimJoust : sélectionne au moins 2 pions humains.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null) return;
			if (manager.Active || manager.Melee.Active)
			{
				Messages.Message("RimJoust : une autre épreuve est en cours.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var map = pawns[0].Map;
			var target = RimJoust.Tournament.ArcheryContest.FindTarget(map);
			if (target == null)
			{
				Messages.Message("RimJoust : pose d'abord une butte de tir.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var spot = RimJoust.Tournament.ArcheryContest.FindSpot(map);
			var line = spot?.Position ?? pawns[0].Position;

			if (!manager.Archery.TryStart(pawns, line, target, map))
				Messages.Message("RimJoust : impossible de lancer le concours de tir.",
					MessageTypeDefOf.RejectInput, historical: false);
		}

		[DebugAction("RimJoust",
			"Lancer un GRAND TOURNOI (2+ pions sélectionnés)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartGrand()
		{
			var pawns = Find.Selector.SelectedObjects.OfType<Pawn>()
				.Where(p => p.RaceProps.Humanlike).ToList();
			if (pawns.Count < 2)
			{
				Messages.Message("RimJoust : sélectionne au moins 2 pions humains.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null) return;

			foreach (var p in pawns)
				WarnIfNotReady(p);

			if (!manager.Grand.TryStart(manager, pawns[0].Map, pawns))
				Messages.Message("RimJoust : impossible de lancer le grand tournoi (épreuve en cours, cooldown, ou lice manquante).",
					MessageTypeDefOf.RejectInput, historical: false);
		}

		[DebugAction("RimJoust",
			"Annuler le grand tournoi",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void CancelGrand()
		{
			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null || !manager.Grand.Active)
			{
				Messages.Message("RimJoust : aucun grand tournoi en cours.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			manager.Grand.Cancel(manager);
		}

		[DebugAction("RimJoust",
			"Annuler le concours de tir",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void CancelArchery()
		{
			var manager = Current.Game.GetComponent<RimJoust.Tournament.TournamentManager>();
			if (manager == null || !manager.Archery.Active)
			{
				Messages.Message("RimJoust : aucun concours de tir en cours.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			manager.Archery.Cancel();
		}
	}
}
