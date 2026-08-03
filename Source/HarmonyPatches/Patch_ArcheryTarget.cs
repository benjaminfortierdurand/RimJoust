using HarmonyLib;
using Verse;

namespace RimJoust.HarmonyPatches
{
	[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
	internal static class Patch_Thing_TakeDamage_ArcheryTarget
	{
		private static void Prefix(Thing __instance, ref DamageInfo dinfo)
		{
			if (__instance?.def != RimJoustDefOf.RimJoust_ArcheryTarget) return;

			var contest = RimJoust.Tournament.TournamentManager.Instance?.Archery;
			if (contest == null || !contest.Active) return;

			if (dinfo.Instigator is Pawn archer)
				contest.RegisterHit(archer);
			dinfo.SetAmount(0f);
		}
	}
}
