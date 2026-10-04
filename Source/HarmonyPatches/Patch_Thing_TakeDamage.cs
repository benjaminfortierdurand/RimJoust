using GiddyUp;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimJoust.HarmonyPatches
{
	[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
	internal static class Patch_Thing_TakeDamage_JoustingLance
	{
		private const float ShieldDismountMultiplier = 0.5f;
		private const int FallStunTicks = 180;

		private const float ArmorDismountEffect = 1.0f;
		private const float MinArmorDismountMult = 0.35f;

		private static readonly bool VerboseLogging = false;

		private static readonly SoundDef _knightFallSound =
			DefDatabase<SoundDef>.GetNamedSilentFail("Roof_Collapse");

		private static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
		{
			try
			{
				TryJoustHit(__instance, dinfo, __result);
			}
			catch (System.Exception e)
			{
				Log.ErrorOnce("[RimJoust] Erreur dans le patch de coup de lance : " + e, 0x71B05731);
			}
		}

		private static void TryJoustHit(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
		{
			if (__instance is not Pawn victim || !victim.Spawned || victim.Dead) return;

			var weaponDef = dinfo.Weapon;
			if (!weaponDef.IsJoustingLance()) return;
			if (!victim.IsMounted()) return;

			var damageDealt = __result?.totalDamageDealt ?? -1f;

			if (VerboseLogging)
			{
				Log.Message(
					$"[RimJoust:debug] Pion monté {victim.LabelShort} prend {damageDealt:F1} dégâts " +
					$"(arme={weaponDef?.defName ?? "<null>"}, attaquant={dinfo.Instigator?.LabelShort ?? "<null>"})");
			}

			JoustVfx.ImpactLanceHit(victim);
			JoustSounds.Impact(new TargetInfo(victim.Position, victim.Map));

			JoustTraitEffects.NotifyHitTaken(victim);

			var dismountChance = Mathf.Clamp01(RimJoustMod.Settings?.dismountChance ?? 0.45f);

			var bluntArmor = victim.GetStatValue(StatDefOf.ArmorRating_Blunt);
			var armorMult = Mathf.Clamp(1f - bluntArmor * ArmorDismountEffect, MinArmorDismountMult, 1f);
			dismountChance *= armorMult;
			if (VerboseLogging)
				Log.Message($"[RimJoust:debug] → {victim.LabelShort} armure contondante {bluntArmor:P0} → ×{armorMult:F2} (chance {dismountChance:P0})");

			var hasShield = victim.HasJoustingShield();
			if (hasShield)
			{
				dismountChance *= ShieldDismountMultiplier;
				JoustVfx.ShieldDeflect(victim);
				if (VerboseLogging)
					Log.Message($"[RimJoust:debug] → {victim.LabelShort} a un bouclier, chance réduite à {dismountChance:P0}.");
			}

			var tm = RimJoust.Tournament.TournamentManager.Instance;
			var attacker = (dinfo.Instigator as Pawn) ?? tm?.OpponentOf(victim);

			JoustTraitEffects.ApplyMaliciousIntent(attacker, victim, dinfo);
			if (victim.Dead || !victim.Spawned) return;

			var skillGap = JoustingSkillLevel(attacker) - JoustingSkillLevel(victim);
			dismountChance = Mathf.Clamp01(dismountChance * Mathf.Clamp(1f + skillGap * 0.03f, 0.4f, 1.6f));
			dismountChance = Mathf.Clamp01(dismountChance * JoustTraitEffects.DismountFactor(attacker, victim));

			if (!Rand.Chance(dismountChance))
			{
				if (VerboseLogging)
					Log.Message($"[RimJoust:debug] → {damageDealt:F1} dégâts, chance {dismountChance:P0}, raté, passe blanche.");
				Messages.Message(
					"RimJoust.HitButHeldOn".Translate(victim.LabelShort),
					victim, MessageTypeDefOf.NeutralEvent, historical: false);
				return;
			}

			var storage = ExtendedDataStorage.Singleton;
			if (storage == null) return;
			var pawnData = storage.GetExtendedPawnData(victim);
			if (pawnData == null) return;
			var mount = pawnData.Mount;

			if (VerboseLogging)
				Log.Message($"[RimJoust:debug] ✔ DÉSARÇONNEMENT de {victim.LabelShort} (monture={mount?.LabelShort ?? "<null>"})");

			victim.InvoluntaryDismount(mount, pawnData);

			var victimLance = victim.equipment?.Primary;
			if (victimLance != null && victimLance.def.IsJoustingLance())
				victim.equipment.DestroyEquipment(victimLance);

			_knightFallSound?.PlayOneShot(new TargetInfo(victim.Position, victim.Map));
			JoustVfx.KnightFall(victim);

			pawnData.canRide = false;

			victim.jobs?.EndCurrentJob(JobCondition.InterruptForced);

			if (victim.drafter != null)
				victim.drafter.Drafted = false;

			victim.stances?.stunner?.StunFor(FallStunTicks, dinfo.Instigator);

			var hardFall = HediffMaker.MakeHediff(RimJoustDefOf.RimJoust_HardFall, victim);
			hardFall.Severity = 1.0f;
			victim.health?.AddHediff(hardFall);

			var fallDamage = new DamageInfo(
				DamageDefOf.Blunt,
				RimJoustMod.Settings?.fallDamage ?? 2f,
				armorPenetration: 0.1f,
				instigator: mount
			);
			JoustingLanceUtility.ApplyJoustHitRegion(ref fallDamage);
			victim.TakeDamage(fallDamage);

			Messages.Message(
				"RimJoust.Unhorsed".Translate(victim.LabelShort),
				victim,
				MessageTypeDefOf.NeutralEvent
			);

			RimJoust.Tournament.TournamentManager.Instance
				?.NotifyKnightEliminated(victim, mount);
		}

		private static int JoustingSkillLevel(Pawn p)
		{
			return JoustSkill.LevelOf(p);
		}

	}
}
