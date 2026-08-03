using System.Collections.Generic;
using RimJoust.Tournament;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.UI
{
	public class CompProperties_TournamentBoard : CompProperties
	{
		public CompProperties_TournamentBoard() { compClass = typeof(CompTournamentBoard); }
	}

	public class CompTournamentBoard : ThingComp
	{
		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (respawningAfterLoad) return;
			TournamentManager.Instance?.MaybeShowIntroLetter();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			var tm = TournamentManager.Instance;
			if (tm == null) yield break;

			var icon = ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_ChallengeJoust", reportFailure: false)
				?? TexCommand.Attack;

			if (parent.def == RimJoustDefOf.RimJoust_MeleeSpot)
			{
				if (tm.Melee.Active)
				{
					yield return new Command_Action
					{
						defaultLabel = "RimJoust.MeleeCancelLabel".Translate(),
						defaultDesc = "RimJoust.MeleeCancelDesc".Translate(),
						icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false) ?? icon,
						action = () => tm.Melee.Cancel(),
					};
				}
				else
				{
					var startMelee = new Command_Action
					{
						defaultLabel = "RimJoust.MeleeStartLabel".Translate(),
						defaultDesc = "RimJoust.MeleeStartDesc".Translate(),
						icon = icon,
						action = () =>
						{
							var map = parent.Map;
							var fighters = new System.Collections.Generic.List<Pawn>();
							foreach (var c in map.mapPawns.FreeColonistsSpawned)
								if (tm.IsJouster(c) && !c.Dead && !c.Downed)
									fighters.Add(c);
							if (fighters.Count < 2)
							{
								Messages.Message("RimJoust.MeleeNeedFighters".Translate(),
									MessageTypeDefOf.RejectInput, historical: false);
								return;
							}
							tm.Melee.TryStart(fighters, Tournament.MeleeBrawl.FindCenter(map, fighters), map);
						},
					};
					if (tm.Active)
					{
						startMelee.Disabled = true;
						startMelee.disabledReason = "RimJoust.MeleeBlockedByTournament".Translate();
					}
					else if (tm.Archery.Active || tm.Grand.Active)
					{
						startMelee.Disabled = true;
						startMelee.disabledReason = "RimJoust.EventBlocked".Translate();
					}
					yield return startMelee;
				}
			}

			if (parent.def == RimJoustDefOf.RimJoust_ArcherySpot)
			{
				if (tm.Archery.Active)
				{
					yield return new Command_Action
					{
						defaultLabel = "RimJoust.ArcheryCancelLabel".Translate(),
						defaultDesc = "RimJoust.ArcheryCancelDesc".Translate(),
						icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false) ?? icon,
						action = () => tm.Archery.Cancel(),
					};
				}
				else
				{
					var startArchery = new Command_Action
					{
						defaultLabel = "RimJoust.ArcheryStartLabel".Translate(),
						defaultDesc = "RimJoust.ArcheryStartDesc".Translate(),
						icon = icon,
						action = () =>
						{
							var map = parent.Map;
							var target = Tournament.ArcheryContest.FindTarget(map);
							if (target == null)
							{
								Messages.Message("RimJoust.ArcheryNeedTarget".Translate(),
									MessageTypeDefOf.RejectInput, historical: false);
								return;
							}
							var archers = new System.Collections.Generic.List<Pawn>();
							foreach (var c in map.mapPawns.FreeColonistsSpawned)
								if (tm.IsJouster(c) && !c.Dead && !c.Downed)
									archers.Add(c);
							if (archers.Count < 2)
							{
								Messages.Message("RimJoust.ArcheryNeedFighters".Translate(),
									MessageTypeDefOf.RejectInput, historical: false);
								return;
							}
							tm.Archery.TryStart(archers, parent.Position, target, map);
						},
					};
					if (tm.Active || tm.Melee.Active || tm.Grand.Active)
					{
						startArchery.Disabled = true;
						startArchery.disabledReason = "RimJoust.EventBlocked".Translate();
					}
					yield return startArchery;
				}
			}

			yield return new Command_Action
			{
				defaultLabel = "RimJoust.GuideGizmoLabel".Translate(),
				defaultDesc = "RimJoust.GuideGizmoDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", reportFailure: false) ?? icon,
				action = Window_JoustGuide.Open,
			};

			yield return new Command_Toggle
			{
				defaultLabel = "RimJoust.RoleGizmosLabel".Translate(),
				defaultDesc = "RimJoust.RoleGizmosDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_SquireToggle", reportFailure: false) ?? icon,
				isActive = () => tm.ShowRoleGizmos,
				toggleAction = () => tm.ToggleRoleGizmos(),
			};

			yield return new Command_Toggle
			{
				defaultLabel = "RimJoust.GuestArrivalsLabel".Translate(),
				defaultDesc = "RimJoust.GuestArrivalsDesc".Translate(),
				icon = icon,
				isActive = () => tm.AllowGuestArrivals,
				toggleAction = () => tm.ToggleGuestArrivals(),
			};

			if (tm.Active)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimJoust.BoardGizmoLabel".Translate(),
					defaultDesc = "RimJoust.BoardGizmoDesc".Translate(),
					icon = icon,
					action = Window_TournamentBracket.Open,
				};

				yield return new Command_Action
				{
					defaultLabel = "RimJoust.CancelTournamentLabel".Translate(),
					defaultDesc = "RimJoust.CancelTournamentDesc".Translate(),
					icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false) ?? icon,
					action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"RimJoust.CancelTournamentConfirm".Translate(),
						() => tm.CancelTournament(),
						destructive: true)),
				};
			}
			else if (tm.Grand.Active)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimJoust.GrandCancelLabel".Translate(),
					defaultDesc = "RimJoust.GrandCancelDesc".Translate(),
					icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false) ?? icon,
					action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"RimJoust.GrandCancelConfirm".Translate(),
						() => tm.Grand.Cancel(tm),
						destructive: true)),
				};

				if (tm.HasResults || tm.PastTournaments.Count > 0)
					yield return new Command_Action
					{
						defaultLabel = "RimJoust.ViewResultsLabel".Translate(),
						defaultDesc = "RimJoust.ViewResultsDesc".Translate(),
						icon = icon,
						action = Window_TournamentBracket.Open,
					};
			}
			else
			{
				LessonAutoActivator.TeachOpportunity(RimJoustDefOf.RimJoust_Concept_Lists, OpportunityType.Important);

				var organize = new Command_Action
				{
					defaultLabel = "RimJoust.StartTournamentLabel".Translate(),
					defaultDesc = "RimJoust.StartTournamentDesc".Translate(),
					icon = icon,
					action = () => tm.TryStartTournamentFromMap(parent.Map),
				};
				if (tm.TournamentOnCooldown)
				{
					organize.Disabled = true;
					organize.disabledReason = "RimJoust.TournamentCooldownGizmo".Translate(Mathf.CeilToInt(tm.DaysUntilTournament));
				}
				yield return organize;

				var grand = new Command_Action
				{
					defaultLabel = "RimJoust.GrandStartLabel".Translate(),
					defaultDesc = "RimJoust.GrandStartDesc".Translate(),
					icon = icon,
					action = () => tm.TryStartGrandFromMap(parent.Map),
				};
				if (tm.TournamentOnCooldown)
				{
					grand.Disabled = true;
					grand.disabledReason = "RimJoust.TournamentCooldownGizmo".Translate(Mathf.CeilToInt(tm.DaysUntilTournament));
				}
				yield return grand;

				if (tm.HasResults || tm.PastTournaments.Count > 0)
					yield return new Command_Action
					{
						defaultLabel = "RimJoust.ViewResultsLabel".Translate(),
						defaultDesc = "RimJoust.ViewResultsDesc".Translate(),
						icon = icon,
						action = Window_TournamentBracket.Open,
					};
			}
		}
	}
}
