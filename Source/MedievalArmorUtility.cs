using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimJoust
{
	public static class MedievalArmorUtility
	{
		private static List<ThingDef> _medievalApparel;

		public static void EquipBestArmor(Pawn pawn)
		{
			if (pawn?.apparel == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return;
			if (AlreadyDressedMedieval(pawn)) return;

			var legs = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Legs");

			var body = PickArmor(pawn, BodyPartGroupDefOf.Torso, outerOnly: true, minSharp: 0.25f);
			if (body == null) return;

			var helmet = PickArmor(pawn, BodyPartGroupDefOf.UpperHead, outerOnly: true, minSharp: 0.2f)
				?? PickArmor(pawn, BodyPartGroupDefOf.FullHead, outerOnly: true, minSharp: 0.2f);
			var legArmor = (legs != null && !Covers(body, legs))
				? PickArmor(pawn, legs, outerOnly: true, minSharp: 0.25f) : null;

			pawn.apparel.DestroyAll();

			WearNamed(pawn, "Apparel_BasicShirt");
			WearNamed(pawn, "Apparel_Pants");
			Wear(pawn, legArmor);
			Wear(pawn, body);
			Wear(pawn, helmet);
			TryAddHouseColors(pawn);
		}

		private static void TryAddHouseColors(Pawn pawn)
		{
			var faction = pawn.Faction;
			if (faction == null) return;
			var tabard = PickTabard(pawn);
			if (tabard == null) return;
			try
			{
				var app = (Apparel)ThingMaker.MakeThing(tabard, GoodStuff(tabard));
				app.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
				pawn.apparel.Wear(app, dropReplacedApparel: false);
				app.TryGetComp<CompColorable>()?.SetColor(faction.Color);
			}
			catch (System.Exception e)
			{
				Log.WarningOnce("[RimJoust] tabard : " + e.Message, tabard.shortHash ^ 0x7A);
			}
		}

		private static ThingDef PickTabard(Pawn pawn)
		{
			foreach (var d in MedievalApparel())
			{
				if (!Wearable(pawn, d) || !Covers(d, BodyPartGroupDefOf.Torso)) continue;
				if (!d.HasComp(typeof(CompColorable))) continue;
				if (!d.apparel.layers.Contains(ApparelLayerDefOf.Overhead)) continue;
				return d;
			}
			return null;
		}

		private static bool AlreadyDressedMedieval(Pawn pawn)
		{
			var worn = pawn.apparel.WornApparel;
			if (worn == null || worn.Count == 0) return false;
			var coversTorso = false;
			for (var i = 0; i < worn.Count; i++)
			{
				var def = worn[i].def;
				if (def.techLevel > TechLevel.Medieval) return false;
				if (Covers(def, BodyPartGroupDefOf.Torso)) coversTorso = true;
			}
			return coversTorso;
		}

		private static List<ThingDef> MedievalApparel()
		{
			if (_medievalApparel != null) return _medievalApparel;
			_medievalApparel = new List<ThingDef>();
			foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (!d.IsApparel || d.apparel?.bodyPartGroups == null || d.apparel.layers == null) continue;
				if (d.techLevel > TechLevel.Medieval) continue;
				if (d.destroyOnDrop) continue;
				_medievalApparel.Add(d);
			}
			return _medievalApparel;
		}

		private static ThingDef PickArmor(Pawn pawn, BodyPartGroupDef group, bool outerOnly, float minSharp)
		{
			if (group == null) return null;
			ThingDef best = null;
			var bestScore = float.MinValue;
			foreach (var d in MedievalApparel())
			{
				if (!Wearable(pawn, d) || !Covers(d, group)) continue;
				if (outerOnly && !HasOuterLayer(d)) continue;
				var stuff = d.MadeFromStuff ? ThingDefOf.Steel : null;
				var sharp = d.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp, stuff);
				if (sharp < minSharp) continue;
				var score = sharp + (IsFromMedievalMod(d) ? 100f : 0f);
				if (score > bestScore) { bestScore = score; best = d; }
			}
			return best;
		}

		private static bool IsFromMedievalMod(ThingDef d)
		{
			var id = d.modContentPack?.PackageId;
			return id != null && id.IndexOf("medieval", System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool Wearable(Pawn pawn, ThingDef d)
		{
			if (d.apparel.gender != Gender.None && d.apparel.gender != pawn.gender) return false;
			return ApparelUtility.HasPartsToWear(pawn, d);
		}

		private static bool Covers(ThingDef d, BodyPartGroupDef group)
			=> d.apparel?.bodyPartGroups != null && d.apparel.bodyPartGroups.Contains(group);

		private static bool HasOuterLayer(ThingDef d)
		{
			var layers = d.apparel.layers;
			for (var i = 0; i < layers.Count; i++)
				if (layers[i] != ApparelLayerDefOf.OnSkin && layers[i] != ApparelLayerDefOf.Belt)
					return true;
			return false;
		}

		private static void WearNamed(Pawn pawn, string defName)
			=> Wear(pawn, DefDatabase<ThingDef>.GetNamedSilentFail(defName));

		private static void Wear(Pawn pawn, ThingDef def)
		{
			if (def == null || !Wearable(pawn, def)) return;
			try
			{
				var apparel = (Apparel)ThingMaker.MakeThing(def, GoodStuff(def));
				apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
				pawn.apparel.Wear(apparel, dropReplacedApparel: false);
			}
			catch (System.Exception e)
			{
				Log.WarningOnce("[RimJoust] impossible d'équiper " + def.defName + " : " + e.Message, def.shortHash);
			}
		}

		private static ThingDef GoodStuff(ThingDef def)
		{
			if (!def.MadeFromStuff) return null;
			return GenStuff.DefaultStuffFor(def);
		}
	}
}
