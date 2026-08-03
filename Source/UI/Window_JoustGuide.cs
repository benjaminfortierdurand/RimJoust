using UnityEngine;
using Verse;

namespace RimJoust.UI
{
	public class Window_JoustGuide : Window
	{
		private static readonly string[] Sections =
			{ "Lists", "Roles", "Start", "During", "After", "Melee", "Archery", "Grand", "Safety" };

		private int _section;
		private Vector2 _scroll;

		public override Vector2 InitialSize => new Vector2(780f, 580f);

		public Window_JoustGuide()
		{
			doCloseX = true;
			draggable = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Window_JoustGuide>())
				Find.WindowStack.Add(new Window_JoustGuide());
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "RimJoust.Guide.Title".Translate());
			Text.Font = GameFont.Small;

			var body = new Rect(0f, 46f, inRect.width, inRect.height - 46f);
			var navW = 196f;
			var navRect = new Rect(body.x, body.y, navW, body.height);
			var contentRect = new Rect(body.x + navW + 14f, body.y, body.width - navW - 14f, body.height);

			var y = navRect.y;
			for (var i = 0; i < Sections.Length; i++)
			{
				var r = new Rect(navRect.x, y, navRect.width, 38f);
				if (i == _section) Widgets.DrawHighlightSelected(r);
				else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

				Widgets.Label(new Rect(r.x + 8f, r.y + 8f, r.width - 12f, r.height),
					("RimJoust.Guide." + Sections[i] + "Title").Translate());

				if (Widgets.ButtonInvisible(r)) { _section = i; _scroll = Vector2.zero; }
				y += 40f;
			}

			Widgets.DrawMenuSection(contentRect);
			var inner = contentRect.ContractedBy(14f);

			var head = ("RimJoust.Guide." + Sections[_section] + "Title").Translate();
			var text = ("RimJoust.Guide." + Sections[_section] + "Body").Translate();

			Text.Font = GameFont.Medium;
			var headH = Text.CalcHeight(head, inner.width);
			Widgets.Label(new Rect(inner.x, inner.y, inner.width, headH), head);
			Text.Font = GameFont.Small;

			var top = inner.y + headH + 10f;
			var viewRect = new Rect(inner.x, top, inner.width, inner.yMax - top);
			var contentWidth = viewRect.width - 18f;
			var textH = Text.CalcHeight(text, contentWidth);
			var scrollView = new Rect(0f, 0f, contentWidth, Mathf.Max(textH, viewRect.height));

			Widgets.BeginScrollView(viewRect, ref _scroll, scrollView);
			Widgets.Label(new Rect(0f, 0f, contentWidth, textH), text);
			Widgets.EndScrollView();
		}
	}
}
