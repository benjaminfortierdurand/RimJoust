using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using RimJoust.UI;

namespace RimJoust.Buildings
{
	public class Building_JoustSpot : Building
	{
		private bool markerHidden;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			if (def.MadeFromStuff && Stuff == null)
				SetStuffDirect(GenStuff.DefaultStuffFor(def));
			base.SpawnSetup(map, respawningAfterLoad);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref markerHidden, "markerHidden", false);
		}

		public override void Print(SectionLayer layer)
		{
			if (markerHidden) return;
			base.Print(layer);
		}

		public override string GetInspectString()
		{
			var s = base.GetInspectString();
			if (!markerHidden) return s;
			var line = "RimJoust.MarkerHidden".Translate();
			return s.NullOrEmpty() ? line : s + "\n" + line;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (var g in base.GetGizmos()) yield return g;

			yield return new Command_Toggle
			{
				defaultLabel = "RimJoust.ShowSpotLabel".Translate(),
				defaultDesc = "RimJoust.ShowSpotDesc".Translate(),
				icon = RimJoustIcons.ShowMarker,
				isActive = () => !markerHidden,
				toggleAction = () =>
				{
					markerHidden = !markerHidden;
					DirtyMapMesh(Map);
				},
			};
		}
	}
}
