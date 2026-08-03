using Verse;

namespace RimJoust
{
	public class RimJoustSettings : ModSettings
	{
		public float lanceDamage = 6f;
		public float dismountChance = 0.45f;
		public float dangerousHitChance = 0.12f;
		public float fallDamage = 2f;
		public float meleeDamageFactor = 0.75f;
		public int tournamentCooldownDays = 15;

		public bool traitEffects = true;
		public bool autoOpenWindows = true;

		public const float DefLanceDamage = 6f;
		public const float DefDismountChance = 0.45f;
		public const float DefDangerousHitChance = 0.12f;
		public const float DefFallDamage = 2f;
		public const float DefMeleeDamageFactor = 0.75f;
		public const float DefMeleeArenaRadius = 10f;
		public const int DefTournamentCooldownDays = 15;

		public const bool DefTraitEffects = true;
		public const bool DefAutoOpenWindows = true;

		public void ResetToDefaults()
		{
			lanceDamage = DefLanceDamage;
			dismountChance = DefDismountChance;
			dangerousHitChance = DefDangerousHitChance;
			fallDamage = DefFallDamage;
			meleeDamageFactor = DefMeleeDamageFactor;
			tournamentCooldownDays = DefTournamentCooldownDays;
			traitEffects = DefTraitEffects;
			autoOpenWindows = DefAutoOpenWindows;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref lanceDamage, "lanceDamage", DefLanceDamage);
			Scribe_Values.Look(ref dismountChance, "dismountChance", DefDismountChance);
			Scribe_Values.Look(ref dangerousHitChance, "dangerousHitChance", DefDangerousHitChance);
			Scribe_Values.Look(ref fallDamage, "fallDamage", DefFallDamage);
			Scribe_Values.Look(ref meleeDamageFactor, "meleeDamageFactor", DefMeleeDamageFactor);
			Scribe_Values.Look(ref tournamentCooldownDays, "tournamentCooldownDays", DefTournamentCooldownDays);
			Scribe_Values.Look(ref traitEffects, "traitEffects", DefTraitEffects);
			Scribe_Values.Look(ref autoOpenWindows, "autoOpenWindows", DefAutoOpenWindows);
		}
	}
}
