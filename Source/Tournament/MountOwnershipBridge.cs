using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GiddyUp;
using Verse;

namespace RimJoust.Tournament
{
	public static class MountOwnershipBridge
	{
		private static bool _resolved;
		private static PropertyInfo _componentProp;
		private static MethodInfo _getAssigned;

		public static bool Available
		{
			get
			{
				Resolve();
				return _componentProp != null && _getAssigned != null;
			}
		}

		private static void Resolve()
		{
			if (_resolved) return;
			_resolved = true;
			try
			{
				var utility = GenTypes.GetTypeInAnyAssembly("CompanionMounts.BondedMountUtility");
				if (utility == null) return;
				_componentProp = utility.GetProperty("Component", BindingFlags.Public | BindingFlags.Static);
				if (_componentProp == null) return;
				_getAssigned = _componentProp.PropertyType.GetMethod("GetAssignedMountsForRider",
					BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Pawn) }, null);
			}
			catch (Exception e)
			{
				Log.Warning("[RimJoust] pont Mount Ownership indisponible : " + e.Message);
				_componentProp = null;
				_getAssigned = null;
			}
		}

		public static Pawn AssignedMountFor(Pawn rider)
		{
			if (rider == null || !Available) return null;
			try
			{
				var component = _componentProp.GetValue(null, null);
				if (component == null) return null;
				if (!(_getAssigned.Invoke(component, new object[] { rider }) is IEnumerable mounts)) return null;
				foreach (var o in mounts)
				{
					if (!(o is Pawn mount)) continue;
					if (mount.Dead || !mount.Spawned || mount.Map != rider.Map) continue;
					if (mount.IsMountedAnimal()) continue;
					return mount;
				}
			}
			catch (Exception e)
			{
				Log.Warning("[RimJoust] lecture de la monture attitrée : " + e.Message);
			}
			return null;
		}

		public static int MountAssignedRiders(Map map, Func<Pawn, bool> selector)
		{
			if (map == null || selector == null || !Available) return 0;
			var mounted = 0;
			foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
			{
				if (p == null || p.Dead || p.Downed || p.IsMounted()) continue;
				if (!selector(p)) continue;
				var mount = AssignedMountFor(p);
				if (mount == null) continue;
				try
				{
					p.GoMount(mount, MountUtility.GiveJobMethod.Instant);
					if (p.IsMounted()) mounted++;
				}
				catch (Exception e)
				{
					Log.Warning("[RimJoust] mise en selle de " + p.LabelShort + " : " + e.Message);
				}
			}
			return mounted;
		}
	}
}
