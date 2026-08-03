using System.Collections.Generic;
using GiddyUp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimJoust.Jobs
{
	public class JobDriver_TrainQuintain : JobDriver
	{
		private Building Quintain => job.GetTarget(TargetIndex.A).Thing as Building;

		private const int TrainTicks = 2500;
		private const int StrikeInterval = 100;
		private const float StrikeXp = 40f;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
			=> pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => !pawn.IsMounted());

			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

			var train = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = TrainTicks };
			train.handlingFacing = true;
			train.tickAction = () =>
			{
				if (Quintain != null) pawn.rotationTracker.FaceTarget(Quintain);
				if (pawn.IsHashIntervalTick(StrikeInterval)) Strike();
			};
			train.WithProgressBarToilDelay(TargetIndex.A);
			yield return train;
		}

		private void Strike()
		{
			(Quintain as RimJoust.Buildings.Building_Quintain)?.Kick();
			if (Quintain != null)
			{
				JoustSounds.Impact(new TargetInfo(Quintain.Position, Quintain.Map));
				FleckMaker.ThrowDustPuff(Quintain.DrawPos, Quintain.Map, 1.1f);
			}
			JoustSkill.Learn(pawn, StrikeXp, direct: false);

			var skill = JoustLevel();
			if (Rand.Chance(Mathf.Lerp(0.25f, 0.02f, skill / 20f)))
				Mishap(skill);
		}

		private void Mishap(int skill)
		{
			FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 1.4f, new Color(0.82f, 0.74f, 0.58f));

			var dmg = new DamageInfo(DamageDefOf.Blunt, Rand.Range(2f, 5f), armorPenetration: 0f, instigator: null);
			dmg.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
			pawn.TakeDamage(dmg);

			if (skill <= 4 && pawn.IsMounted() && Rand.Chance(0.35f))
				Unhorse();
		}

		private void Unhorse()
		{
			var data = ExtendedDataStorage.Singleton?.GetExtendedPawnData(pawn);
			if (data != null) pawn.InvoluntaryDismount(data.Mount, data);
			pawn.stances?.stunner?.StunFor(60, null);
			Messages.Message("RimJoust.QuintainUnhorsed".Translate(pawn.LabelShort),
				pawn, MessageTypeDefOf.NeutralEvent, historical: false);
			EndJobWith(JobCondition.InterruptForced);
		}

		private int JoustLevel() => JoustSkill.LevelOf(pawn);
	}
}
