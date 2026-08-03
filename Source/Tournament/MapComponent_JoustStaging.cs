using UnityEngine;
using Verse;

namespace RimJoust.Tournament
{
	public class MapComponent_JoustStaging : MapComponent
	{
		public MapComponent_JoustStaging(Map map) : base(map) { }

		private static readonly Color CountdownGold = new Color(0.96f, 0.86f, 0.45f);

		public override void MapComponentOnGUI()
		{
			if (Event.current.type != EventType.Repaint) return;

			var tm = TournamentManager.Instance;
			if (tm == null) return;

			if (tm.TryGetStartCountdown(map, out var secondsLeft, out var phase) && secondsLeft > 0)
			{
				var scale = 7f * (1f + 0.35f * phase);
				DrawBigCenter(secondsLeft.ToString(), scale, Mathf.Clamp01(1.35f - phase));
			}
			else if (tm.TryGetStartGo(map, out var fade))
			{
				DrawBigCenter("RimJoust.JoustGo".Translate(), 3.2f, Mathf.Clamp01(fade + 0.25f));
			}
			else if (tm.TryGetCeremony(map, out var ctext, out var cfade))
			{
				DrawBigCenter(ctext, 2.3f, Mathf.Clamp01(cfade));
			}
		}

		private static void DrawBigCenter(string label, float scale, float alpha)
		{
			var matrixBak = GUI.matrix;
			var colorBak = GUI.color;
			var fontBak = Text.Font;
			var anchorBak = Text.Anchor;

			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;

			var pivot = new Vector2(Verse.UI.screenWidth / 2f, Verse.UI.screenHeight * 0.5f);
			GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
			var rect = new Rect(pivot.x - 200f, pivot.y - 40f, 400f, 80f);

			GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
			Widgets.Label(new Rect(rect.x + 1.2f, rect.y + 1.2f, rect.width, rect.height), label);

			GUI.color = new Color(CountdownGold.r, CountdownGold.g, CountdownGold.b, alpha);
			Widgets.Label(rect, label);

			GUI.matrix = matrixBak;
			GUI.color = colorBak;
			Text.Font = fontBak;
			Text.Anchor = anchorBak;
		}
	}
}
