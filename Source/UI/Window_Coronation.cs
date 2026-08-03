using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.UI
{
	public enum CoronationKind { Wife, Admiration, Scandal }

	public class Window_Coronation : Window
	{
		private readonly Pawn _champion, _queen, _jilted;
		private readonly CoronationKind _kind;

		private static readonly Color Gold = new Color(0.95f, 0.86f, 0.55f);
		private static readonly Color Scarlet = new Color(0.85f, 0.35f, 0.32f);
		private static readonly Color PortraitBg = new Color(0.12f, 0.12f, 0.12f, 0.6f);

		public Window_Coronation(Pawn champion, Pawn queen, Pawn jilted, CoronationKind kind)
		{
			_champion = champion; _queen = queen; _jilted = jilted; _kind = kind;
			draggable = true;
			doCloseX = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = true;
			forcePause = false;
			preventCameraMotion = false;
		}

		public override Vector2 InitialSize => new Vector2(540f, _kind == CoronationKind.Scandal ? 524f : 440f);

		public static void Show(Pawn champion, Pawn queen, Pawn jilted, CoronationKind kind)
		{
			if (champion == null || queen == null) return;
			Find.WindowStack.Add(new Window_Coronation(champion, queen, jilted, kind));
		}

		public override void DoWindowContents(Rect inRect)
		{
			var y = inRect.y;

			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperCenter;
			GUI.color = Gold;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "RimJoust.CoronationTitle".Translate());
			GUI.color = Color.white;
			y += 38f;

			Text.Font = GameFont.Small;
			if (_kind == CoronationKind.Scandal)
			{
				GUI.color = Scarlet;
				Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "RimJoust.CoronationScandalTag".Translate());
				GUI.color = Color.white;
			}
			y += 26f;
			Text.Anchor = TextAnchor.UpperLeft;

			var pSize = 108f;
			var gap = 44f;
			var rowW = pSize * 2 + gap;
			var startX = inRect.x + (inRect.width - rowW) / 2f;
			DrawPortrait(new Rect(startX, y, pSize, pSize), _champion, _champion.LabelShortCap, Color.white);
			DrawPortrait(new Rect(startX + pSize + gap, y, pSize, pSize), _queen, _queen.LabelShortCap, Gold);
			y += pSize + 24f;

			if (_kind == CoronationKind.Scandal && _jilted != null)
			{
				var js = 74f;
				DrawPortrait(new Rect(inRect.x + (inRect.width - js) / 2f, y, js, js), _jilted, _jilted.LabelShortCap, Scarlet);
				y += js + 22f;
			}

			string text;
			if (_kind == CoronationKind.Scandal)
				text = "RimJoust.CoronationScandal".Translate(_champion.LabelShort, _jilted != null ? _jilted.LabelShort : "?", _queen.LabelShort);
			else if (_kind == CoronationKind.Admiration)
				text = "RimJoust.CoronationAdmire".Translate(_champion.LabelShort, _queen.LabelShort);
			else
				text = "RimJoust.CoronationWife".Translate(_champion.LabelShort, _queen.LabelShort);

			var textRect = new Rect(inRect.x + 10f, y, inRect.width - 20f, inRect.yMax - y - 44f);
			GUI.color = new Color(0.9f, 0.88f, 0.82f);
			Widgets.Label(textRect, text);
			GUI.color = Color.white;

			var bw = 170f;
			if (Widgets.ButtonText(new Rect(inRect.x + (inRect.width - bw) / 2f, inRect.yMax - 36f, bw, 32f), "RimJoust.CoronationClose".Translate()))
				Close();
		}

		private void DrawPortrait(Rect rect, Pawn pawn, string label, Color labelColor)
		{
			Widgets.DrawBoxSolid(rect, PortraitBg);
			if (pawn != null && !pawn.Destroyed)
			{
				var tex = PortraitsCache.Get(pawn, new Vector2(rect.width, rect.height), Rot4.South, default, 1.2f);
				GUI.DrawTexture(rect, tex);
			}
			var lr = new Rect(rect.x - 12f, rect.yMax + 2f, rect.width + 24f, 20f);
			Text.Anchor = TextAnchor.UpperCenter;
			GUI.color = labelColor;
			Widgets.Label(lr, label);
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}
