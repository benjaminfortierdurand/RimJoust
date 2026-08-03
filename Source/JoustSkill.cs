using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust
{
	public static class JoustSkill
	{
		public const float StrikeWeight = 0.6f;
		public const float RideWeight = 0.4f;

		public static int LevelOf(Pawn p)
		{
			if (p?.skills == null) return 0;
			var strike = p.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
			var ride = p.skills.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
			return Mathf.RoundToInt(strike * StrikeWeight + ride * RideWeight);
		}

		public static void Learn(Pawn p, float xp, bool direct)
		{
			if (p?.skills == null || xp <= 0f) return;
			p.skills.Learn(SkillDefOf.Melee, xp * StrikeWeight, direct);
			p.skills.Learn(SkillDefOf.Animals, xp * RideWeight, direct);
		}

		public static void EnsureLevel(Pawn p, int minLevel, Passion passion)
		{
			if (p?.skills == null) return;
			Bump(p.skills.GetSkill(SkillDefOf.Melee), minLevel, passion);
			Bump(p.skills.GetSkill(SkillDefOf.Animals), minLevel, passion);
		}

		private static void Bump(SkillRecord rec, int minLevel, Passion passion)
		{
			if (rec == null || rec.TotallyDisabled) return;
			if (rec.Level < minLevel) rec.Level = minLevel;
			if (rec.passion < passion) rec.passion = passion;
		}
	}
}
