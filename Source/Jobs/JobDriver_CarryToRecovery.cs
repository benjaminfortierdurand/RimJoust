using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimJoust.Jobs
{
	public class JobDriver_CarryToRecovery : JobDriver
	{
		private Pawn Takee => job.GetTarget(TargetIndex.A).Thing as Pawn;
		private IntVec3 DropCell => job.GetTarget(TargetIndex.B).Cell;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed: false);
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);

			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
				.FailOnDestroyedOrNull(TargetIndex.A);

			yield return Toils_Haul.StartCarryThing(TargetIndex.A);

			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			var drop = new Toil
			{
				defaultCompleteMode = ToilCompleteMode.Instant,
			};
			drop.initAction = () =>
			{
				pawn.carryTracker.TryDropCarriedThing(DropCell, ThingPlaceMode.Near, out _);
			};
			yield return drop;
		}
	}
}
