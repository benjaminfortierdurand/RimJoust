using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimJoust
{
	public static class JoustStarter
	{
		public static bool TryStart(Pawn a, Pawn b)
		{
			if (a == null || b == null || a == b) return false;

			if (a.Map != b.Map)
			{
				Reject("RimJoust.PawnsNeedSameMap".Translate());
				return false;
			}

			var map = a.Map;
			var spotA = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotA).FirstOrDefault();
			var spotB = map.listerThings.ThingsOfDef(RimJoustDefOf.RimJoust_SpotB).FirstOrDefault();
			if (spotA == null || spotB == null)
			{
				Reject("RimJoust.NeedFlagsPlaced".Translate());
				return false;
			}

			if (a.drafter != null) a.drafter.Drafted = true;
			if (b.drafter != null) b.drafter.Drafted = true;

			ComputeLaneSigns(a, b, spotA.Position, spotB.Position, out var aLaneSign, out var bLaneSign);

			StartJoustPass(a, b, spotA.Position, spotB.Position, aLaneSign);
			StartJoustPass(b, a, spotA.Position, spotB.Position, bLaneSign);

			Messages.Message("RimJoust.JoustStarted".Translate(a.LabelShort, b.LabelShort),
				new LookTargets(new[] { a, b }), MessageTypeDefOf.NeutralEvent, historical: false);

			return true;
		}

		private static void StartJoustPass(Pawn charger, Pawn opponent, IntVec3 spotA, IntVec3 spotB, int laneSign)
		{
			var job = JobMaker.MakeJob(RimJoustDefOf.RimJoust_JoustPass);
			job.targetA = new LocalTargetInfo(spotA);
			job.targetB = new LocalTargetInfo(spotB);
			job.targetC = opponent;
			job.count = laneSign;
			charger.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		private static void ComputeLaneSigns(Pawn a, Pawn b, IntVec3 spotA, IntVec3 spotB, out int aSign, out int bSign)
		{
			var dx = spotB.x - spotA.x;
			var dz = spotB.z - spotA.z;
			var horizontal = System.Math.Abs(dx) >= System.Math.Abs(dz);

			int aProj, bProj;
			if (horizontal)
			{
				aProj = a.Position.z - spotA.z;
				bProj = b.Position.z - spotA.z;
			}
			else
			{
				aProj = a.Position.x - spotA.x;
				bProj = b.Position.x - spotA.x;
			}

			if (aProj != bProj)
				aSign = aProj > bProj ? 1 : -1;
			else
				aSign = a.thingIDNumber < b.thingIDNumber ? 1 : -1;
			bSign = -aSign;
		}

		private static void Reject(string msg)
		{
			Messages.Message(msg, MessageTypeDefOf.RejectInput, historical: false);
		}
	}
}
