using Verse;

namespace RimJoust
{
	[StaticConstructorOnStartup]
	internal static class RimJoustStartup
	{
		static RimJoustStartup()
		{
			if (RimJoustDefOf.RimJoust_TrainQuintain != null)
				GiddyUp.Jobs.JobDriver_Mounted.SetAllowedJob(RimJoustDefOf.RimJoust_TrainQuintain, false);
		}
	}
}
