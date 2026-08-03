using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimJoust
{
	public class RimJoustMod : Mod
	{
		public const string HarmonyId = "forti.rimjoust";

		public static RimJoustSettings Settings { get; private set; }

		public RimJoustMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<RimJoustSettings>();

			var harmony = new Harmony(HarmonyId);
			harmony.PatchAll();
			Log.Message("[RimJoust] Mod chargé, prêt à jouter.");
		}

		public override string SettingsCategory() => "RimJoust";

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var l = new Listing_Standard();
			l.Begin(inRect);

			l.Label("RimJoust.Settings.Intro".Translate());
			l.GapLine();

			l.Label("RimJoust.Settings.LanceDamage".Translate() + ": " + Settings.lanceDamage.ToString("F0"));
			Settings.lanceDamage = Mathf.Round(l.Slider(Settings.lanceDamage, 3f, 25f));

			l.Label("RimJoust.Settings.DismountChance".Translate() + ": " + (Settings.dismountChance * 100f).ToString("F0") + "%");
			Settings.dismountChance = l.Slider(Settings.dismountChance, 0.05f, 1f);

			l.Label("RimJoust.Settings.DangerousHitChance".Translate() + ": " + (Settings.dangerousHitChance * 100f).ToString("F0") + "%");
			Settings.dangerousHitChance = l.Slider(Settings.dangerousHitChance, 0f, 1f);
			l.Label("RimJoust.Settings.DangerousHitHint".Translate(), tooltip: null);

			l.Label("RimJoust.Settings.FallDamage".Translate() + ": " + Settings.fallDamage.ToString("F0"));
			Settings.fallDamage = Mathf.Round(l.Slider(Settings.fallDamage, 0f, 12f));

			l.Label("RimJoust.Settings.MeleeDamage".Translate() + ": " + (Settings.meleeDamageFactor * 100f).ToString("F0") + "%");
			Settings.meleeDamageFactor = l.Slider(Settings.meleeDamageFactor, 0.25f, 1.5f);

			l.Label("RimJoust.Settings.TournamentCooldown".Translate() + ": " + Settings.tournamentCooldownDays);
			Settings.tournamentCooldownDays = Mathf.RoundToInt(l.Slider(Settings.tournamentCooldownDays, 0f, 60f));

			l.GapLine();
			l.CheckboxLabeled("RimJoust.Settings.TraitEffects".Translate(), ref Settings.traitEffects,
				"RimJoust.Settings.TraitEffectsHint".Translate());
			l.CheckboxLabeled("RimJoust.Settings.AutoPopups".Translate(), ref Settings.autoOpenWindows,
				"RimJoust.Settings.AutoPopupsHint".Translate());

			l.GapLine();
			if (l.ButtonText("RimJoust.Settings.OpenGuide".Translate()))
				RimJoust.UI.Window_JoustGuide.Open();

			l.Gap();
			if (l.ButtonText("RimJoust.Settings.Reset".Translate()))
				Settings.ResetToDefaults();

			l.End();
		}
	}
}
