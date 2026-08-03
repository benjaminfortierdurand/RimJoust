using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust
{
	public static class JoustTraitEffects
	{
		private const float WimpForfeitChance = 0.25f;
		private const float BloodlustMaimChance = 0.35f;
		private const float PsychopathKillChance = 0.20f;

		private static TraitDef _brawler, _tough, _nimble, _wimp, _masochist, _bloodlust, _psychopath, _nerves, _naturalMood;
		private static bool _resolved;

		private static void Resolve()
		{
			if (_resolved) return;
			_resolved = true;
			_brawler = TraitNamed("Brawler");
			_tough = TraitNamed("Tough");
			_nimble = TraitNamed("Nimble");
			_wimp = TraitNamed("Wimp");
			_masochist = TraitNamed("Masochist");
			_bloodlust = TraitNamed("Bloodlust");
			_psychopath = TraitNamed("Psychopath");
			_nerves = TraitNamed("Nerves");
			_naturalMood = TraitNamed("NaturalMood");
		}

		private static TraitDef TraitNamed(string defName) => DefDatabase<TraitDef>.GetNamedSilentFail(defName);

		private static bool Has(Pawn p, TraitDef t) => t != null && p?.story?.traits != null && p.story.traits.HasTrait(t);

		private static int Degree(Pawn p, TraitDef t)
		{
			if (t == null || p?.story?.traits == null) return 0;
			return p.story.traits.GetTrait(t)?.Degree ?? 0;
		}

		private static bool Enabled => RimJoustMod.Settings?.traitEffects ?? true;

		public static float StrengthBonus(Pawn p)
		{
			if (!Enabled || p == null) return 0f;
			Resolve();
			var b = 0f;
			if (Has(p, _brawler)) b += 1.0f;
			if (Has(p, _tough)) b += 0.6f;
			if (Has(p, _nimble)) b += 0.5f;
			return b;
		}

		public static float DismountFactor(Pawn attacker, Pawn defender)
		{
			if (!Enabled) return 1f;
			Resolve();
			var f = 1f;
			if (Has(attacker, _brawler)) f *= 1.25f;
			if (Has(defender, _tough)) f *= 0.55f;
			if (Has(defender, _nimble)) f *= 0.70f;
			var nd = Degree(defender, _nerves);
			if (nd >= 2) f *= 0.85f;
			else if (nd == 1) f *= 0.92f;
			else if (nd == -1) f *= 1.10f;
			else if (nd <= -2) f *= 1.20f;
			return f;
		}

		public static bool TryForfeit(Pawn a, Pawn b, out Pawn forfeiter)
		{
			forfeiter = null;
			if (!Enabled) return false;
			Resolve();
			if (Has(a, _wimp) && Rand.Chance(WimpForfeitChance)) { forfeiter = a; return true; }
			if (Has(b, _wimp) && Rand.Chance(WimpForfeitChance)) { forfeiter = b; return true; }
			return false;
		}

		public static void NotifyHitTaken(Pawn victim)
		{
			if (!Enabled || victim?.needs?.mood == null) return;
			Resolve();
			if (!Has(victim, _masochist)) return;
			if (RimJoustDefOf.RimJoust_MasochistThrill == null) return;
			victim.needs.mood.thoughts.memories.TryGainMemory(RimJoustDefOf.RimJoust_MasochistThrill);
		}

		public static void ApplyMaliciousIntent(Pawn attacker, Pawn victim, DamageInfo lanceHit)
		{
			if (!Enabled || attacker == null || victim == null || victim.Dead || !victim.Spawned) return;
			Resolve();

			var instigator = victim.Faction == attacker.Faction ? attacker : null;

			if (Has(attacker, _psychopath))
			{
				if (Rand.Chance(PsychopathKillChance))
				{
					var d = new DamageInfo(DamageDefOf.Stab, Rand.Range(20f, 30f), armorPenetration: 0.35f, instigator: instigator);
					d.SetBodyRegion(BodyPartHeight.Top, BodyPartDepth.Outside);
					victim.TakeDamage(d);
					Messages.Message("RimJoust.PsychopathKill".Translate(attacker.LabelShort),
						victim, MessageTypeDefOf.ThreatSmall, historical: false);
					return;
				}
			}

			if (Has(attacker, _bloodlust))
			{
				if (Rand.Chance(BloodlustMaimChance))
				{
					var d = new DamageInfo(DamageDefOf.Cut, Rand.Range(12f, 18f), armorPenetration: 0.25f, instigator: instigator);
					d.SetBodyRegion(Rand.Bool ? BodyPartHeight.Bottom : BodyPartHeight.Middle, BodyPartDepth.Outside);
					victim.TakeDamage(d);
					Messages.Message("RimJoust.BloodlustMaim".Translate(attacker.LabelShort),
						victim, MessageTypeDefOf.NeutralEvent, historical: false);
				}
			}
		}

		public static void GiveBoutLossThought(Pawn loser)
		{
			if (!Enabled || loser?.needs?.mood == null) return;
			Resolve();
			if (RimJoustDefOf.RimJoust_LostJoust == null) return;
			var degree = Degree(loser, _naturalMood);
			if (degree >= 2) return;
			int stage = degree == 1 ? 0 : (degree == 0 ? 1 : 2);
			loser.needs.mood.thoughts.memories.TryGainMemory(
				ThoughtMaker.MakeThought(RimJoustDefOf.RimJoust_LostJoust, stage));
		}
	}
}
