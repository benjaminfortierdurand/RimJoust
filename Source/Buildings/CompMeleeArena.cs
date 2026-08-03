using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.Buildings
{
	public class CompProperties_MeleeArena : CompProperties
	{
		public CompProperties_MeleeArena() { compClass = typeof(CompMeleeArena); }
	}

	public class CompMeleeArena : ThingComp
	{
		public const float MinRadius = 5f;
		public const float MaxRadius = 30f;

		private float _radius = RimJoustSettings.DefMeleeArenaRadius;
		private List<IntVec3> _regionCells;
		private HashSet<IntVec3> _regionSet;

		public float Radius => _radius;

		public bool HasRegion => _regionCells != null && _regionCells.Count > 0;

		public IReadOnlyList<IntVec3> RegionCells => _regionCells;

		private HashSet<IntVec3> RegionSet
		{
			get
			{
				if (_regionSet == null && _regionCells != null)
					_regionSet = new HashSet<IntVec3>(_regionCells);
				return _regionSet;
			}
		}

		public bool InArena(IntVec3 c)
		{
			if (HasRegion) return RegionSet.Contains(c);
			return parent.Spawned && c.DistanceTo(parent.Position) <= _radius + 0.5f;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref _radius, "arenaRadius", RimJoustSettings.DefMeleeArenaRadius);
			Scribe_Collections.Look(ref _regionCells, "regionCells", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit) _regionSet = null;
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			if (!parent.Spawned) return;
			if (HasRegion) GenDraw.DrawFieldEdges(_regionCells);
			else GenDraw.DrawRadiusRing(parent.Position, _radius);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Command_Action
			{
				defaultLabel = "RimJoust.ArenaRadiusPlus".Translate(_radius.ToString("F0")),
				defaultDesc = "RimJoust.ArenaRadiusDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Buttons/Plus", reportFailure: false) ?? TexCommand.Attack,
				action = () => { _radius = Mathf.Min(MaxRadius, _radius + 1f); ClearRegion(); },
			};
			yield return new Command_Action
			{
				defaultLabel = "RimJoust.ArenaRadiusMinus".Translate(_radius.ToString("F0")),
				defaultDesc = "RimJoust.ArenaRadiusDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Buttons/Minus", reportFailure: false) ?? TexCommand.Attack,
				action = () => { _radius = Mathf.Max(MinRadius, _radius - 1f); ClearRegion(); },
			};
			yield return new Command_Action
			{
				defaultLabel = "RimJoust.ArenaRadiusAuto".Translate(),
				defaultDesc = "RimJoust.ArenaRadiusAutoDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Buttons/AutoRebuild", reportFailure: false)
					?? ContentFinder<Texture2D>.Get("UI/Buttons/Plus", reportFailure: false)
					?? TexCommand.Attack,
				action = AutoFit,
			};
		}

		private void ClearRegion()
		{
			_regionCells = null;
			_regionSet = null;
		}

		private void AutoFit()
		{
			var map = parent.Map;
			if (map == null || !parent.Spawned) return;

			var origin = parent.Position;
			var limit = MaxRadius + 2f;
			var visited = new HashSet<IntVec3> { origin };
			var reached = new List<IntVec3> { origin };
			var queue = new Queue<IntVec3>();
			queue.Enqueue(origin);
			var maxDist = 0f;
			var leaked = false;
			var dirs = new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West };

			while (queue.Count > 0)
			{
				var c = queue.Dequeue();
				foreach (var d in dirs)
				{
					var n = c + d;
					if (!visited.Add(n)) continue;
					if (!n.InBounds(map)) { leaked = true; continue; }
					if (!n.Walkable(map)) continue;
					if (n.GetEdifice(map) is Building_Door) continue;
					var dist = n.DistanceTo(origin);
					if (dist > limit) { leaked = true; continue; }
					if (dist > maxDist) maxDist = dist;
					reached.Add(n);
					queue.Enqueue(n);
				}
			}

			if (leaked)
			{
				Messages.Message("RimJoust.ArenaNotEnclosed".Translate(Mathf.RoundToInt(MaxRadius)),
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			_regionCells = reached;
			_regionSet = null;
			_radius = Mathf.Clamp(Mathf.Ceil(maxDist), MinRadius, MaxRadius);
			Messages.Message("RimJoust.ArenaAutoFit".Translate(reached.Count),
				MessageTypeDefOf.NeutralEvent, historical: false);
		}
	}
}
