using Verse;

namespace RimJoust.Tournament
{
	public class TournamentRecord : IExposable
	{
		public string date;
		public string championName;
		public string championFaction;
		public string queenName;
		public string kind;
		public int participants;
		public int rounds;

		public void ExposeData()
		{
			Scribe_Values.Look(ref date, "date");
			Scribe_Values.Look(ref kind, "kind");
			Scribe_Values.Look(ref championName, "championName");
			Scribe_Values.Look(ref championFaction, "championFaction");
			Scribe_Values.Look(ref queenName, "queenName");
			Scribe_Values.Look(ref participants, "participants");
			Scribe_Values.Look(ref rounds, "rounds");
		}
	}
}
