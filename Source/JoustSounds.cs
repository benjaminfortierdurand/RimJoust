using RimWorld;
using Verse;
using Verse.Sound;

namespace RimJoust
{
	public static class JoustSounds
	{
		private static bool _init;
		private static SoundDef _impact;

		private static void Ensure()
		{
			if (_init) return;
			_init = true;
			_impact = DefDatabase<SoundDef>.GetNamedSilentFail("Pawn_Melee_Punch_HitPawn");
		}

		public static void CountdownTick() => SoundDefOf.Tick_High.PlayOneShotOnCamera();

		public static void Horn(TargetInfo at) => RimJoustDefOf.RimJoust_JoustHorn?.PlayOneShot(at);

		public static void Victory(TargetInfo at) => RimJoustDefOf.RimJoust_VictoryFanfare?.PlayOneShot(at);

		public static void Impact(TargetInfo at)
		{
			Ensure();
			_impact?.PlayOneShot(at);
		}
	}
}
