using Verse;

namespace RimJoust.Tournament
{
	public class BoutRecord : IExposable
	{
		public int round;
		public Pawn a;
		public Pawn b;
		public Pawn winner;
		public bool bye;

		public BoutRecord() { }

		public BoutRecord(int round, Pawn a, Pawn b, Pawn winner, bool bye = false)
		{
			this.round = round;
			this.a = a;
			this.b = b;
			this.winner = winner;
			this.bye = bye;
		}

		public Pawn Loser => bye ? null : (winner == a ? b : a);

		public void ExposeData()
		{
			Scribe_Values.Look(ref round, "round");
			Scribe_Values.Look(ref bye, "bye");
			Scribe_References.Look(ref a, "a");
			Scribe_References.Look(ref b, "b");
			Scribe_References.Look(ref winner, "winner");
		}
	}
}
