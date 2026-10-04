using System;
using System.Reflection;
using Verse;

namespace RimJoust.Tournament
{
	public static class SimpleSidearmsBridge
	{
		private static bool _resolved;
		private static MethodInfo _getMemory;
		private static MethodInfo _setForced;
		private static MethodInfo _unsetForced;
		private static MethodInfo _forget;
		private static PropertyInfo _forcedDrafted;
		private static ConstructorInfo _pairCtor;
		private static FieldInfo _pairThing;
		private static FieldInfo _pairStuff;

		public static bool Available
		{
			get
			{
				Resolve();
				return _getMemory != null && _setForced != null && _unsetForced != null && _pairCtor != null;
			}
		}

		private static void Resolve()
		{
			if (_resolved) return;
			_resolved = true;
			try
			{
				var memoryType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");
				var pairType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.ThingDefStuffDefPair");
				if (memoryType == null || pairType == null) return;

				_getMemory = memoryType.GetMethod("GetMemoryCompForPawn", BindingFlags.Public | BindingFlags.Static,
					null, new[] { typeof(Pawn), typeof(bool) }, null);
				_setForced = memoryType.GetMethod("SetWeaponAsForced", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { pairType, typeof(bool) }, null);
				_unsetForced = memoryType.GetMethod("UnsetForcedWeapon", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { typeof(bool) }, null);
				_forget = memoryType.GetMethod("ForgetSidearmMemory", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { pairType }, null);
				_forcedDrafted = memoryType.GetProperty("ForcedWeaponWhileDrafted", BindingFlags.Public | BindingFlags.Instance);
				_pairCtor = pairType.GetConstructor(new[] { typeof(ThingDef), typeof(ThingDef) });
				_pairThing = pairType.GetField("thing", BindingFlags.Public | BindingFlags.Instance);
				_pairStuff = pairType.GetField("stuff", BindingFlags.Public | BindingFlags.Instance);
			}
			catch (Exception e)
			{
				Log.Warning("[RimJoust] pont Simple Sidearms indisponible : " + e.Message);
				_getMemory = null;
				_setForced = null;
				_unsetForced = null;
				_pairCtor = null;
			}
		}

		private static object MemoryOf(Pawn p)
		{
			if (p == null || !Available) return null;
			try { return _getMemory.Invoke(null, new object[] { p, true }); }
			catch { return null; }
		}

		private static object MakePair(ThingDef thing, ThingDef stuff)
			=> _pairCtor.Invoke(new object[] { thing, stuff });

		public static void Pin(Pawn p, ThingWithComps weapon, SidearmPin pin)
		{
			if (weapon == null || pin == null) return;
			var memory = MemoryOf(p);
			if (memory == null) return;
			try
			{
				pin.prevDraftedDef = null;
				pin.prevDraftedStuff = null;
				var prev = _forcedDrafted?.GetValue(memory, null);
				if (prev != null && _pairThing != null)
				{
					pin.prevDraftedDef = (_pairThing.GetValue(prev) as ThingDef)?.defName;
					pin.prevDraftedStuff = (_pairStuff?.GetValue(prev) as ThingDef)?.defName;
				}
				_setForced.Invoke(memory, new[] { MakePair(weapon.def, weapon.Stuff), (object)true });
				pin.pinned = true;
			}
			catch (Exception e)
			{
				Log.Warning("[RimJoust] Simple Sidearms : impossible de verrouiller l'arme de " + p.LabelShort + " (" + e.Message + ").");
			}
		}

		public static void Unpin(Pawn p, SidearmPin pin, ThingWithComps given)
		{
			if (pin == null || !pin.pinned) return;
			pin.pinned = false;
			var memory = MemoryOf(p);
			if (memory == null) return;
			try
			{
				if (given != null && _forget != null)
					_forget.Invoke(memory, new[] { MakePair(given.def, given.Stuff) });

				var prevDef = pin.prevDraftedDef.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(pin.prevDraftedDef);
				if (prevDef != null)
				{
					var prevStuff = pin.prevDraftedStuff.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(pin.prevDraftedStuff);
					_setForced.Invoke(memory, new[] { MakePair(prevDef, prevStuff), (object)true });
				}
				else
				{
					_unsetForced.Invoke(memory, new object[] { true });
				}
			}
			catch (Exception e)
			{
				Log.Warning("[RimJoust] Simple Sidearms : impossible de libérer l'arme de " + p.LabelShort + " (" + e.Message + ").");
			}
		}
	}
}
