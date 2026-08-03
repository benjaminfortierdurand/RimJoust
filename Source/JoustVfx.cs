using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust
{
	public static class JoustVfx
	{
		private static readonly Color WoodChipColor = new Color(0.52f, 0.34f, 0.16f, 1f);
		private static readonly Color DustColor = new Color(0.82f, 0.74f, 0.58f, 1f);

		public static void ImpactLanceHit(Pawn victim)
		{
			if (victim == null || !victim.Spawned) return;
			var pos = victim.DrawPos;
			var map = victim.Map;
			for (var i = 0; i < 4; i++)
			{
				var offset = Rand.InsideUnitCircleVec3 * 0.4f;
				FleckMaker.ThrowDustPuffThick(pos + offset, map, Rand.Range(0.5f, 0.9f), WoodChipColor);
			}
		}

		public static void KnightFall(Pawn victim)
		{
			if (victim == null || !victim.Spawned) return;
			var pos = victim.DrawPos;
			var map = victim.Map;
			FleckMaker.ThrowDustPuffThick(pos, map, 2.0f, DustColor);
			for (var i = 0; i < 4; i++)
			{
				var offset = Rand.InsideUnitCircleVec3 * 0.7f;
				FleckMaker.ThrowDustPuffThick(pos + offset, map, Rand.Range(0.8f, 1.3f), DustColor);
			}
		}

		public static void ShieldDeflect(Pawn victim)
		{
			if (victim == null || !victim.Spawned) return;
			var pos = victim.DrawPos;
			var map = victim.Map;
			for (var i = 0; i < 6; i++)
			{
				var offset = Rand.InsideUnitCircleVec3 * 0.35f;
				FleckMaker.ThrowMicroSparks(pos + offset, map);
			}
		}
	}
}
