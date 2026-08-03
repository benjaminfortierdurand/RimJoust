using RimWorld;
using Verse;

namespace RimJoust
{
	[DefOf]
	public static class RimJoustDefOf
	{
		public static JobDef RimJoust_JoustPass;
		public static JobDef RimJoust_CarryToRecovery;
		public static JobDef RimJoust_WatchJoust;
		public static JobDef RimJoust_TrainQuintain;

		public static ThingDef RimJoust_JoustingLance;
		public static ThingDef RimJoust_TourneyMace;
		public static ThingDef RimJoust_SpotA;
		public static ThingDef RimJoust_SpotB;
		public static ThingDef RimJoust_MeleeSpot;
		public static ThingDef RimJoust_ArcherySpot;
		public static ThingDef RimJoust_ArcheryTarget;
		public static ThingDef RimJoust_RecoverySpot;
		public static ThingDef RimJoust_DeadSpot;
		public static ThingDef RimJoust_SquirePost;
		public static ThingDef RimJoust_KnightPost;
		public static ThingDef RimJoust_HorseSpot;
		public static ThingDef RimJoust_LordSeat;
		public static ThingDef RimJoust_ConsortSeat;
		public static ThingDef RimJoust_Quintain;

		public static HediffDef RimJoust_HardFall;
		public static HediffDef RimJoust_ChampionRenown;
		public static HediffDef RimJoust_TourneyMount;

		public static ConceptDef RimJoust_Concept_Lists;
		public static ConceptDef RimJoust_Concept_Jouster;
		public static ConceptDef RimJoust_Concept_Squire;
		public static ConceptDef RimJoust_Concept_Guests;
		public static ConceptDef RimJoust_Concept_Bets;

		public static SoundDef RimJoust_JoustHorn;
		public static SoundDef RimJoust_VictoryFanfare;

		public static ThoughtDef RimJoust_TournamentChampion;
		public static ThoughtDef RimJoust_CrownedQueen;
		public static ThoughtDef RimJoust_TourneyFavor;
		public static ThoughtDef RimJoust_WatchedJoust;
		public static ThoughtDef RimJoust_MasochistThrill;
		public static ThoughtDef RimJoust_LostJoust;
		public static ThoughtDef RimJoust_PassedOver;
		public static ThoughtDef RimJoust_JiltedAtTourney;
		public static ThoughtDef RimJoust_RivalCrowned;

		static RimJoustDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(RimJoustDefOf));
		}
	}
}
