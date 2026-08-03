using Verse;

namespace RimJoust
{
	public class HediffCompProperties_RandomSeverityPerDay : HediffCompProperties
	{
		public FloatRange recoveryHoursRange = new FloatRange(2f, 10f);
		public bool showHoursToRecover = true;

		public HediffCompProperties_RandomSeverityPerDay()
		{
			compClass = typeof(HediffComp_RandomSeverityPerDay);
		}
	}

	public class HediffComp_RandomSeverityPerDay : HediffComp
	{
		private const int SeverityUpdateInterval = 200;

		private float severityPerDay = 0f;
		private bool initialized = false;

		public HediffCompProperties_RandomSeverityPerDay Props
			=> (HediffCompProperties_RandomSeverityPerDay)props;

		private void EnsureInitialized()
		{
			if (initialized) return;

			float hours = Props.recoveryHoursRange.RandomInRange;
			if (hours < 0.1f) hours = 0.1f;

			severityPerDay = -(24f / hours);
			initialized = true;
		}

		public override void CompPostMake()
		{
			base.CompPostMake();
			EnsureInitialized();
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			EnsureInitialized();

			if (parent.pawn != null && parent.pawn.IsHashIntervalTick(SeverityUpdateInterval))
			{
				severityAdjustment += severityPerDay * (SeverityUpdateInterval / 60000f);
			}
		}

		public override string CompTipStringExtra
		{
			get
			{
				if (!Props.showHoursToRecover || severityPerDay >= 0f) return null;

				float hoursLeft = parent.Severity / -severityPerDay * 24f;
				if (hoursLeft <= 0f) return null;

				return "RimJoust.RecoverIn".Translate(hoursLeft.ToString("F1"));
			}
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_Values.Look(ref severityPerDay, "rimJoustSeverityPerDay", 0f);
			Scribe_Values.Look(ref initialized, "rimJoustInitialized", false);
		}
	}
}
