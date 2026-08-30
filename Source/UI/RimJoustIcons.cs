using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.UI
{
	[StaticConstructorOnStartup]
	public static class RimJoustIcons
	{
		public static readonly Texture2D Tournament = Load("RimJoust_Tournament");
		public static readonly Texture2D GrandTournament = Load("RimJoust_GrandTournament");
		public static readonly Texture2D Melee = Load("RimJoust_Melee");
		public static readonly Texture2D Archery = Load("RimJoust_Archery");
		public static readonly Texture2D Results = Load("RimJoust_Results");
		public static readonly Texture2D Guests = Load("RimJoust_Guests");
		public static readonly Texture2D RoleToggle = Load("RimJoust_RoleToggle");
		public static readonly Texture2D JousterToggle = Load("RimJoust_JousterToggle");
		public static readonly Texture2D SquireToggle = Load("RimJoust_SquireToggle");
		public static readonly Texture2D EnrollGuest = Load("RimJoust_EnrollGuest");
		public static readonly Texture2D Guide =
			ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", reportFailure: false) ?? Load("RimJoust_Tournament");
		public static readonly Texture2D Cancel =
			ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false) ?? Load("RimJoust_Tournament");

		private static Texture2D Load(string name)
			=> ContentFinder<Texture2D>.Get("UI/Commands/" + name, reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_ChallengeJoust", reportFailure: false)
				?? TexCommand.Attack;
	}
}
