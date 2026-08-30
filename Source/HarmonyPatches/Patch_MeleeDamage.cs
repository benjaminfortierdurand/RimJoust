using HarmonyLib;
using Verse;

namespace RimJoust.HarmonyPatches
{
	[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
	internal static class Patch_Thing_TakeDamage_MeleeBrawl
	{
		private static void Prefix(Thing __instance, ref DamageInfo dinfo)
		{
			var melee = RimJoust.Tournament.TournamentManager.Instance?.Melee;
			if (melee == null || !melee.Active) return;

			if (!(__instance is Pawn victim)) return;
			if (!(dinfo.Instigator is Pawn attacker) || attacker == victim) return;
			if (!melee.Covers(victim) || !melee.Covers(attacker)) return;

			var factor = (RimJoustMod.Settings?.meleeDamageFactor ?? RimJoustSettings.DefMeleeDamageFactor)
				* melee.FatigueFactor;
			if (factor > 0.999f && factor < 1.001f) return;
			dinfo.SetAmount(dinfo.Amount * factor);
		}
	}
}
