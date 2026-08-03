using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimJoust.Jobs
{
	public class JobDriver_WatchJoust : JobDriver
	{
		private const int GiveThoughtAfterTicks = 1250;

		private int _watchTicks;
		private bool _gaveThought;

		private IntVec3 FocusCell => job.targetB.Cell;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed: false);
			return true;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _watchTicks, "watchTicks");
			Scribe_Values.Look(ref _gaveThought, "gaveThought");
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() =>
			{
				var tm = RimJoust.Tournament.TournamentManager.Instance;
				if (tm == null) return true;
				switch (job.count)
				{
					case 1: return !tm.Active;
					case 2: return !tm.Melee.Active;
					case 3: return !tm.Archery.Active;
					default: return !tm.Active && !tm.Melee.Active && !tm.Archery.Active && !tm.Grand.Active;
				}
			});
			this.FailOn(() => pawn.Map != null && pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);

			if (job.targetA.HasThing) this.FailOnDespawnedOrNull(TargetIndex.A);
			yield return job.targetA.HasThing
				? Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell)
				: Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

			var watch = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			watch.handlingFacing = true;
			watch.initAction = () => pawn.pather?.StopDead();
			watch.tickAction = () =>
			{
				var food = pawn.needs?.food;
				var rest = pawn.needs?.rest;
				if ((food != null && food.CurLevelPercentage < 0.08f)
					|| (rest != null && rest.CurLevelPercentage < 0.08f))
				{
					EndJobWith(JobCondition.InterruptForced);
					return;
				}

				pawn.rotationTracker.FaceCell(FocusCell);
				var rate = job.def.joyGainRate <= 0f ? 1f : job.def.joyGainRate;
				pawn.needs?.joy?.GainJoy(0.00015f * rate, JoyKindDefOf.Social);

				_watchTicks++;
				if (!_gaveThought && _watchTicks >= GiveThoughtAfterTicks)
				{
					if (RimJoust.RimJoustDefOf.RimJoust_WatchedJoust != null)
						pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(RimJoust.RimJoustDefOf.RimJoust_WatchedJoust);
					_gaveThought = true;
				}
			};
			yield return watch;
		}
	}
}
