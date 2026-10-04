using System.Collections.Generic;
using GiddyUp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimJoust.Buildings
{
	[StaticConstructorOnStartup]
	public class Building_Quintain : Building
	{
		private float _armAngle;
		private float _angularVel;

		private static readonly Material ArmMat =
			MaterialPool.MatFrom("Things/Building/RimJoust_Quintain_Arm", ShaderDatabase.Cutout);

		public void Kick() => _angularVel = Mathf.Clamp(_angularVel + 14f, -34f, 34f);

		protected override void Tick()
		{
			base.Tick();
			if (Mathf.Abs(_angularVel) < 0.02f) { _angularVel = 0f; return; }
			_armAngle = Mathf.Repeat(_armAngle + _angularVel, 360f);
			_angularVel *= 0.97f;
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);

			var pos = drawLoc;
			pos.y += 0.04f;
			var matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(_armAngle, Vector3.up), new Vector3(2.6f, 1f, 2.6f));
			Graphics.DrawMesh(MeshPool.plane10, matrix, ArmMat, 0);
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
		{
			foreach (var o in base.GetFloatMenuOptions(selPawn)) yield return o;

			if (selPawn == null || !selPawn.IsColonistPlayerControlled) yield break;
			if (selPawn.WorkTagIsDisabled(WorkTags.Violent)) yield break;
			if (!selPawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly)) yield break;

			if (!selPawn.IsMounted())
			{
				yield return new FloatMenuOption("RimJoust.TrainQuintainNeedMount".Translate(), null);
				yield break;
			}

			yield return new FloatMenuOption("RimJoust.TrainQuintain".Translate(), () =>
			{
				var job = JobMaker.MakeJob(RimJoustDefOf.RimJoust_TrainQuintain, this);
				selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			});
		}
	}
}
