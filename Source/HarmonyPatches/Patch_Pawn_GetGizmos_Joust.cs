using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.HarmonyPatches
{
	[StaticConstructorOnStartup]
	internal static class JoustGizmoIcon
	{
		private static Texture2D _cached;
		public static Texture2D Get()
		{
			if (_cached == null)
			{
				_cached = ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_ChallengeJoust", reportFailure: false)
					?? TexCommand.Attack;
			}
			return _cached;
		}
	}

	[StaticConstructorOnStartup]
	internal static class SquireGizmoIcon
	{
		private static Texture2D _cached;
		public static Texture2D Get()
		{
			if (_cached == null)
			{
				_cached = ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_SquireToggle", reportFailure: false)
					?? TexCommand.Attack;
			}
			return _cached;
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
	internal static class Patch_Pawn_GetGizmos_Joust
	{
		private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
		{
			foreach (var g in values) yield return g;

			var map = __instance.Map;
			if (map == null) yield break;
			var tm = RimJoust.Tournament.TournamentManager.Instance;

			if (tm == null || !tm.ShowRoleGizmos) yield break;

			if (__instance.IsColonistPlayerControlled
				&& map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_RecoverySpot).Count > 0)
			{
				LessonAutoActivator.TeachOpportunity(RimJoustDefOf.RimJoust_Concept_Squire, OpportunityType.GoodToKnow);
				yield return new Command_Toggle
				{
					defaultLabel = "RimJoust.SquireToggleLabel".Translate(),
					defaultDesc = "RimJoust.SquireToggleDesc".Translate(),
					icon = SquireGizmoIcon.Get(),
					isActive = () => tm.IsSquire(__instance),
					toggleAction = () => tm.ToggleSquire(__instance),
				};
			}

			if (!tm.Active && __instance.IsColonistPlayerControlled
				&& map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).Count > 0)
			{
				LessonAutoActivator.TeachOpportunity(RimJoustDefOf.RimJoust_Concept_Jouster, OpportunityType.GoodToKnow);
				yield return new Command_Toggle
				{
					defaultLabel = "RimJoust.JousterToggleLabel".Translate(),
					defaultDesc = "RimJoust.JousterToggleDesc".Translate(),
					icon = JoustGizmoIcon.Get(),
					isActive = () => tm.IsJouster(__instance),
					toggleAction = () => tm.ToggleJouster(__instance),
				};
			}

			if (!tm.Active && tm.CanEnrollGuest(__instance)
				&& map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).Count > 0)
			{
				LessonAutoActivator.TeachOpportunity(RimJoustDefOf.RimJoust_Concept_Guests, OpportunityType.GoodToKnow);
				var enroll = new Command_Toggle
				{
					defaultLabel = "RimJoust.EnrollGuestLabel".Translate(),
					defaultDesc = "RimJoust.EnrollGuestDesc".Translate(),
					icon = JoustGizmoIcon.Get(),
					isActive = () => tm.IsEnrolledGuest(__instance),
					toggleAction = () => tm.ToggleGuestEnrollment(__instance),
				};

				var lvl = RimJoust.Tournament.GuestKnightUtility.JoustLevelOf(__instance);
				if (lvl < RimJoust.Tournament.GuestKnightUtility.MinJoustLevel)
				{
					enroll.Disabled = true;
					enroll.disabledReason = "RimJoust.EnrollGuestTooWeak".Translate(
						lvl, RimJoust.Tournament.GuestKnightUtility.MinJoustLevel);
				}
				yield return enroll;
			}
		}
	}
}
