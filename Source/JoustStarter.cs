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

			StartJoustPass(a, b, spotA.Position, spotB.Position);
			StartJoustPass(b, a, spotA.Position, spotB.Position);

			Messages.Message("RimJoust.JoustStarted".Translate(a.LabelShort, b.LabelShort),
				new LookTargets(new[] { a, b }), MessageTypeDefOf.NeutralEvent, historical: false);

			return true;
		}

		private static void StartJoustPass(Pawn charger, Pawn opponent, IntVec3 spotA, IntVec3 spotB)
		{
			var job = JobMaker.MakeJob(RimJoustDefOf.RimJoust_JoustPass);
			job.targetA = new LocalTargetInfo(spotA);
			job.targetB = new LocalTargetInfo(spotB);
			job.targetC = opponent;
			charger.jobs.StartJob(job, JobCondition.InterruptForced);
		}


		private static void Reject(string msg)
		{
			Messages.Message(msg, MessageTypeDefOf.RejectInput, historical: false);
		}
	}
}
