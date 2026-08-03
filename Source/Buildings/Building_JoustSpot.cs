using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

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

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (var g in base.GetGizmos()) yield return g;

			yield return new Command_Toggle
			{
				defaultLabel = "RimJoust.HideSpotLabel".Translate(),
				defaultDesc = "RimJoust.HideSpotDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/RimJoust_HideSpot", reportFailure: false)
					?? TexCommand.ForbidOff,
				isActive = () => markerHidden,
				toggleAction = () =>
				{
					markerHidden = !markerHidden;
					DirtyMapMesh(Map);
				},
			};
		}
	}
}
