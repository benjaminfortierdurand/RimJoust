using HarmonyLib;
using RimWorld;
using Verse;

namespace RimJoust.HarmonyPatches
{
	internal static class JoustGoodwillShield
	{
		public static bool Suppressing;

		public static bool Protects(Pawn member)
		{
			if (member == null) return false;
			if (member.CurJobDef == RimJoustDefOf.RimJoust_JoustPass) return true;
			var tm = RimJoust.Tournament.TournamentManager.Instance;
			if (tm == null) return false;
			if (tm.Melee != null && tm.Melee.Covers(member)) return true;
			return tm.Active && tm.IsParticipant(member);
		}
	}

	[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberTookDamage))]
	internal static class Patch_Faction_MemberTookDamage_Joust
	{
		private static bool Prefix(Pawn member) => !JoustGoodwillShield.Protects(member);
	}

	[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberDied))]
	internal static class Patch_Faction_MemberDied_Joust
	{
		private static void Prefix(Pawn member)
		{
			if (JoustGoodwillShield.Protects(member)) JoustGoodwillShield.Suppressing = true;
		}

		private static void Finalizer()
		{
			JoustGoodwillShield.Suppressing = false;
		}
	}

	[HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
	internal static class Patch_Faction_TryAffectGoodwill_Joust
	{
		private static bool Prefix(ref bool __result)
		{
			if (!JoustGoodwillShield.Suppressing) return true;
			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
		typeof(Thought_Memory), typeof(Pawn))]
	internal static class Patch_MemoryThoughtHandler_HarmedMe_Joust
	{
		private static ThoughtDef _harmedMe;
		private static bool _resolved;

		private static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
		{
			if (newThought == null || otherPawn == null) return true;
			if (!_resolved)
			{
				_resolved = true;
				_harmedMe = DefDatabase<ThoughtDef>.GetNamedSilentFail("HarmedMe");
			}
			if (_harmedMe == null || newThought.def != _harmedMe) return true;
			if (!JoustGoodwillShield.Protects(__instance.pawn) || !JoustGoodwillShield.Protects(otherPawn)) return true;
			return false;
		}
	}
}
