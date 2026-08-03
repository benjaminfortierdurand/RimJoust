using Verse;

namespace RimJoust
{
	public static class JoustingLanceUtility
	{
		public static void ApplyJoustHitRegion(ref DamageInfo dinfo)
		{
			var danger = RimJoustMod.Settings?.dangerousHitChance ?? 0.25f;
			if (!Rand.Chance(danger))
				dinfo.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
		}

		public const string JoustingLanceWeaponTag = "RimJoust_JoustingLance";

		public static bool HasJoustingLance(this Pawn pawn)
		{
			var weapon = pawn?.equipment?.Primary;
			if (weapon == null) return false;
			return IsJoustingLance(weapon.def);
		}

		public static bool IsJoustingLance(this ThingDef def)
		{
			return def?.weaponTags != null && def.weaponTags.Contains(JoustingLanceWeaponTag);
		}

		public static bool HasJoustingShield(this Pawn pawn)
		{
			var worn = pawn?.apparel?.WornApparel;
			if (worn == null) return false;
			for (var i = 0; i < worn.Count; i++)
			{
				if (IsJoustValidShield(worn[i].def)) return true;
			}
			return false;
		}

		private static bool IsJoustValidShield(ThingDef def)
		{
			if (def == null) return false;

			if (def.thingClass?.FullName == "VFECore.Apparel_Shield")
				return true;

			if (def.thingCategories != null)
			{
				for (var i = 0; i < def.thingCategories.Count; i++)
				{
					if (def.thingCategories[i].defName == "DankPyon_Shields") return true;
				}
			}

			return false;
		}
	}
}
