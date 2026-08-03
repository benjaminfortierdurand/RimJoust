using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimJoust
{
	public static class KnightArrivalUtility
	{
		public static bool TryFindFaction(out Faction faction)
		{
			faction = EligibleFactionsInRandomOrder().FirstOrDefault();
			return faction != null;
		}

		public static IEnumerable<Faction> EligibleFactionsInRandomOrder()
		{
			return Find.FactionManager.AllFactionsVisible
				.Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden
					&& !f.HostileTo(Faction.OfPlayer) && GetHumanlikeKind(f) != null)
				.InRandomOrder();
		}

		public static PawnKindDef GetHumanlikeKind(Faction f)
		{
			PawnKindDef best = null, bestAny = null;
			float bestPower = float.MinValue, bestAnyPower = float.MinValue;
			if (f.def.pawnGroupMakers != null)
				foreach (var g in f.def.pawnGroupMakers)
				{
					if (g.options == null) continue;
					foreach (var o in g.options)
					{
						var k = o.kind;
						if (!IsHumanlike(k)) continue;
						if (k.combatPower > bestAnyPower) { bestAnyPower = k.combatPower; bestAny = k; }
						if (k.factionLeader) continue;
						if (k.combatPower > bestPower) { bestPower = k.combatPower; best = k; }
					}
				}
			if (best != null) return best;
			if (bestAny != null) return bestAny;
			return IsHumanlike(f.def.basicMemberKind) ? f.def.basicMemberKind : null;
		}

		private static bool IsHumanlike(PawnKindDef k) => k?.race?.race?.Humanlike ?? false;

		public static Pawn GenerateKnight(Faction faction, int tile)
		{
			var kind = GetHumanlikeKind(faction);
			if (kind == null) return null;

			var req = new PawnGenerationRequest(
				kind, faction, PawnGenerationContext.NonPlayer,
				tile: tile,
				forceGenerateNewPawn: true,
				mustBeCapableOfViolence: true,
				canGeneratePawnRelations: false,
				developmentalStages: DevelopmentalStage.Adult);
			var p = PawnGenerator.GeneratePawn(req);
			if (p == null) return null;

			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 18)
				p.ageTracker.AgeBiologicalTicks = (long)Rand.RangeInclusive(22, 45) * 3600000L;

			if (!CanRenderSilhouette(p))
			{
				if (Prefs.DevMode)
					Log.Message("[RimJoust] chevalier écarté : la race " + (p.def?.defName ?? "?")
						+ " n'a pas de données de silhouette (incompatible rendu 1.6).");
				if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
				Find.WorldPawns.PassToWorld(p, RimWorld.Planet.PawnDiscardDecideMode.Discard);
				return null;
			}

			JoustSkill.EnsureLevel(p, Rand.RangeInclusive(15, 20), Passion.Major);

			MedievalArmorUtility.EquipBestArmor(p);
			return p;
		}

		private static bool CanRenderSilhouette(Pawn p)
		{
			if (p.RaceProps == null || !p.RaceProps.Humanlike) return true;
			try
			{
				var data = p.ageTracker?.CurLifeStage?.silhouetteGraphicData;
				return data?.Graphic != null;
			}
			catch
			{
				return false;
			}
		}
	}
}
