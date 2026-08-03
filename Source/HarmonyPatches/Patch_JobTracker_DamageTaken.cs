using HarmonyLib;
using Verse;
using Verse.AI;

namespace RimJoust.HarmonyPatches
{
	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.Notify_DamageTaken))]
	internal static class Patch_Pawn_JobTracker_Notify_DamageTaken
	{
		private static bool Prefix(Pawn ___pawn)
		{
			return ___pawn?.CurJob?.def != RimJoustDefOf.RimJoust_JoustPass;
		}
	}
}
