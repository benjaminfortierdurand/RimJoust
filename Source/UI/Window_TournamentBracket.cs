using System.Collections.Generic;
using RimJoust.Tournament;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimJoust.UI
{
	public class Window_TournamentBracket : Window
	{
		private enum Tab { Overview, Bracket, Journal, Hall }
		private Tab _tab = Tab.Overview;

		private Vector2 _scroll;
		private Vector2 _bracketScroll;
		private Vector2 _journalScroll;
		private Vector2 _hallScroll;
		private float _viewHeight = 400f;
		private float _hallHeight = 400f;

		private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.55f);
		private static readonly Color HeaderColor = new Color(0.85f, 0.78f, 0.45f);
		private static readonly Color AdvancedColor = new Color(0.50f, 0.80f, 0.45f);
		private static readonly Color EliminatedColor = new Color(0.80f, 0.45f, 0.45f);
		private static readonly Color MutedColor = new Color(0.70f, 0.70f, 0.70f);
		private static readonly Color BarBg = new Color(0.15f, 0.15f, 0.15f, 0.85f);
		private static readonly Color ProgressColor = new Color(0.85f, 0.70f, 0.30f);
		private static readonly Color PortraitBg = new Color(0.12f, 0.12f, 0.12f, 0.60f);
		private static readonly Color CellBg = new Color(0.16f, 0.16f, 0.18f, 0.85f);
		private static readonly Color ConnectorColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
		private static readonly Color TabActiveBg = new Color(0.27f, 0.27f, 0.27f, 0.65f);

		private const float RowH = 30f;
		private const float BannerH = 156f;

		public Window_TournamentBracket()
		{
			draggable = true;
			doCloseX = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
			forcePause = false;
			resizeable = false;
		}

		public override Vector2 InitialSize => new Vector2(620f, 640f);

		protected override void SetInitialSizeAndPosition()
		{
			windowRect = new Rect(Verse.UI.screenWidth - InitialSize.x - 12f, 60f, InitialSize.x, InitialSize.y);
			windowRect = windowRect.Rounded();
		}

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Window_TournamentBracket>())
				Find.WindowStack.Add(new Window_TournamentBracket());
		}

		public override void DoWindowContents(Rect inRect)
		{
			var tm = TournamentManager.Instance;
			if (tm == null) { Close(); return; }
			var hasLive = tm.Active || tm.HasResults;
			if (!hasLive && tm.PastTournaments.Count == 0) { Close(); return; }
			if (!hasLive) _tab = Tab.Hall;

			var y = inRect.y;

			Text.Font = GameFont.Medium;
			GUI.color = TitleColor;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 32f),
				!hasLive ? "RimJoust.TabHall".Translate().ToString()
				: tm.HasResults ? "RimJoust.BoardTitleFinished".Translate().ToString()
				: "RimJoust.BoardTitle".Translate(tm.CurrentRound).ToString());
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			y += 34f;

			if (hasLive)
			{
				GUI.color = MutedColor;
				Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f),
					"RimJoust.BoardSubline".Translate(tm.TotalParticipants, tm.StillInRunning, tm.Eliminated.Count));
				GUI.color = Color.white;
				y += 22f;

				var barRect = new Rect(inRect.x, y, inRect.width, 10f);
				Widgets.DrawBoxSolid(barRect, BarBg);
				Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(tm.Progress), barRect.height), ProgressColor);
				Widgets.DrawBox(barRect);
				y += 16f;

				DrawTabs(new Rect(inRect.x, y, inRect.width, 30f));
				y += 34f;
			}

			var content = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
			switch (_tab)
			{
				case Tab.Bracket: DrawBracket(content, tm); break;
				case Tab.Journal: DrawJournal(content, tm); break;
				case Tab.Hall: DrawHall(content, tm); break;
				default: DrawOverview(content, tm); break;
			}
		}

		private void DrawTabs(Rect rect)
		{
			var w = rect.width / 4f;
			if (TabButton(new Rect(rect.x, rect.y, w, rect.height), "RimJoust.TabOverview".Translate(), _tab == Tab.Overview)) _tab = Tab.Overview;
			if (TabButton(new Rect(rect.x + w, rect.y, w, rect.height), "RimJoust.TabBracket".Translate(), _tab == Tab.Bracket)) _tab = Tab.Bracket;
			if (TabButton(new Rect(rect.x + 2f * w, rect.y, w, rect.height), "RimJoust.TabJournal".Translate(), _tab == Tab.Journal)) _tab = Tab.Journal;
			if (TabButton(new Rect(rect.x + 3f * w, rect.y, w, rect.height), "RimJoust.TabHall".Translate(), _tab == Tab.Hall)) _tab = Tab.Hall;
		}

		private static bool TabButton(Rect r, string label, bool active)
		{
			if (active) Widgets.DrawBoxSolid(r, TabActiveBg);
			Widgets.DrawBox(r);
			if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
			Text.Anchor = TextAnchor.MiddleCenter;
			if (active) GUI.color = TitleColor;
			Widgets.Label(r, label);
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			return Widgets.ButtonInvisible(r);
		}

		private void DrawOverview(Rect rect, TournamentManager tm)
		{
			var y = rect.y;
			if (tm.HasResults) DrawResultsBanner(new Rect(rect.x, y, rect.width, BannerH), tm);
			else DrawCurrentBout(new Rect(rect.x, y, rect.width, BannerH), tm);
			y += BannerH + 6f;

			if (tm.Active && !tm.HasResults && tm.HasGuests)
			{
				DrawBetting(new Rect(rect.x, y, rect.width, 72f), tm);
				y += 78f;
			}

			var bodyRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
			var viewRect = new Rect(0f, 0f, bodyRect.width - 18f, _viewHeight);
			Widgets.BeginScrollView(bodyRect, ref _scroll, viewRect);

			var vy = 0f;
			if (tm.HasResults)
			{
				DrawSection(viewRect.width, ref vy, "RimJoust.BoardChampionSection".Translate(), new List<Pawn> { tm.Champion }, tm, AdvancedColor);
				DrawSection(viewRect.width, ref vy, "RimJoust.BoardEliminated".Translate(tm.Eliminated.Count), tm.Eliminated, tm, EliminatedColor);
			}
			else
			{
				DrawSection(viewRect.width, ref vy, "RimJoust.BoardWaiting".Translate(tm.RemainingThisRound.Count), tm.RemainingThisRound, tm, HeaderColor);
				DrawSection(viewRect.width, ref vy, "RimJoust.BoardAdvanced".Translate(tm.AdvancedThisRound.Count), tm.AdvancedThisRound, tm, AdvancedColor);
				DrawSection(viewRect.width, ref vy, "RimJoust.BoardEliminated".Translate(tm.Eliminated.Count), tm.Eliminated, tm, EliminatedColor);
			}
			_viewHeight = vy;

			Widgets.EndScrollView();
		}

		private static void DrawResultsBanner(Rect rect, TournamentManager tm)
		{
			Widgets.DrawMenuSection(rect);
			var inner = rect.ContractedBy(8f);

			Text.Anchor = TextAnchor.UpperCenter;
			GUI.color = TitleColor;
			Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "RimJoust.ResultsChampion".Translate());
			GUI.color = Color.white;

			var cx = inner.center.x;
			DrawPortrait(new Rect(cx - 32f, inner.y + 24f, 64f, 64f), tm.Champion);

			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inner.x, inner.y + 90f, inner.width, 26f), tm.Champion?.LabelShortCap ?? "?");
			Text.Font = GameFont.Small;

			if (tm.Queen != null)
			{
				GUI.color = MutedColor;
				Widgets.Label(new Rect(inner.x, inner.y + 116f, inner.width, 20f), "RimJoust.ResultsQueen".Translate(tm.Queen.LabelShortCap));
				GUI.color = Color.white;
			}
			Text.Anchor = TextAnchor.UpperLeft;

			LocateOnClick(rect, tm.Champion);
		}

		private static void DrawCurrentBout(Rect rect, TournamentManager tm)
		{
			Widgets.DrawMenuSection(rect);
			var inner = rect.ContractedBy(8f);

			Text.Anchor = TextAnchor.UpperCenter;
			GUI.color = HeaderColor;
			Widgets.Label(new Rect(inner.x, inner.y, inner.width, 20f), "RimJoust.BoardCurrentBout".Translate());
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;

			var rowY = inner.y + 22f;
			var rowH = inner.yMax - rowY;
			var half = (inner.width - 40f) / 2f;
			DrawFighter(new Rect(inner.x, rowY, half, rowH), tm.CurrentBoutA, tm);
			DrawFighter(new Rect(inner.xMax - half, rowY, half, rowH), tm.CurrentBoutB, tm);

			GUI.DrawTexture(new Rect(inner.x + half + 4f, rowY + rowH / 2f - 16f, 32f, 32f), TexCommand.Attack);
		}

		private int _stakeInput = 100;

		private void DrawBetting(Rect rect, TournamentManager tm)
		{
			Widgets.DrawMenuSection(rect);
			var inner = rect.ContractedBy(6f);

			if (tm.CurrentBoutA == null || tm.CurrentBoutB == null)
			{
				Text.Anchor = TextAnchor.MiddleCenter; GUI.color = MutedColor;
				Widgets.Label(inner, "RimJoust.BetWaiting".Translate());
				GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			if (!tm.CurrentBoutHasGuest)
			{
				Text.Anchor = TextAnchor.MiddleCenter; GUI.color = MutedColor;
				Widgets.Label(inner, "RimJoust.BetNoGuest".Translate());
				GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			if (tm.HasActiveBet)
			{
				Text.Anchor = TextAnchor.MiddleCenter; GUI.color = TitleColor;
				Widgets.Label(inner, "RimJoust.BetActive".Translate(tm.BetStake, BetName(tm.BetFighter), tm.BetOdds.ToString("0.0")));
				GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			if (tm.BetsClosed)
			{
				Text.Anchor = TextAnchor.MiddleCenter; GUI.color = MutedColor;
				Widgets.Label(inner, "RimJoust.BetClosed".Translate());
				GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			tm.GetOdds(out var oa, out var ob);

			var line1 = new Rect(inner.x, inner.y, inner.width, 20f);
			GUI.color = HeaderColor;
			Widgets.Label(line1, "RimJoust.BetHeader".Translate());
			GUI.color = MutedColor; Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(line1, "RimJoust.BetSilver".Translate(tm.ColonySilverAvailable()));
			GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;

			var line2 = new Rect(inner.x, inner.y + 24f, inner.width, 26f);
			var minus = new Rect(line2.x, line2.y, 26f, 26f);
			if (Widgets.ButtonText(minus, "-")) _stakeInput = Mathf.Max(10, _stakeInput - 50);
			var stakeRect = new Rect(minus.xMax + 2f, line2.y, 64f, 26f);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(stakeRect, _stakeInput.ToString());
			Text.Anchor = TextAnchor.UpperLeft;
			var plus = new Rect(stakeRect.xMax + 2f, line2.y, 26f, 26f);
			if (Widgets.ButtonText(plus, "+")) _stakeInput += 50;

			var rest = line2.xMax - (plus.xMax + 8f);
			var bw = (rest - 6f) / 2f;
			var bA = new Rect(plus.xMax + 8f, line2.y, bw, 26f);
			var bB = new Rect(bA.xMax + 6f, line2.y, bw, 26f);
			if (Widgets.ButtonText(bA, BetName(tm.CurrentBoutA) + "  x" + oa.ToString("0.0")))
				tm.TryPlaceBet(tm.CurrentBoutA, _stakeInput);
			if (Widgets.ButtonText(bB, BetName(tm.CurrentBoutB) + "  x" + ob.ToString("0.0")))
				tm.TryPlaceBet(tm.CurrentBoutB, _stakeInput);
		}

		private static string BetName(Pawn p) => p == null ? "?" : p.LabelShortCap.ToString();

		private static void DrawFighter(Rect rect, Pawn p, TournamentManager tm)
		{
			if (p == null)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = MutedColor;
				Widgets.Label(rect, "RimJoust.BoardNobody".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

			var cx = rect.center.x;
			DrawPortrait(new Rect(cx - 32f, rect.y, 64f, 64f), p);

			Text.Anchor = TextAnchor.UpperCenter;
			var nameRect = new Rect(rect.x, rect.y + 66f, rect.width, 20f);
			if (p.Dead) GUI.color = EliminatedColor;
			Widgets.Label(nameRect, NameOf(p));
			GUI.color = Color.white;

			GUI.color = MutedColor;
			Widgets.Label(new Rect(rect.x, nameRect.yMax + 2f, rect.width, 18f), "RimJoust.BoardWins".Translate(tm.WinsOf(p)));
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;

			LocateOnClick(rect, p);
		}

		private static void DrawSection(float width, ref float y, string header, IReadOnlyList<Pawn> pawns,
			TournamentManager tm, Color color)
		{
			GUI.color = color;
			Widgets.Label(new Rect(0f, y, width, 22f), header);
			GUI.color = Color.white;
			y += 24f;

			if (pawns == null || pawns.Count == 0)
			{
				GUI.color = MutedColor;
				Widgets.Label(new Rect(12f, y, width - 12f, 22f), "RimJoust.BoardNobody".Translate());
				GUI.color = Color.white;
				y += 24f;
			}
			else
			{
				for (var i = 0; i < pawns.Count; i++)
				{
					DrawPawnRow(new Rect(0f, y, width, RowH), pawns[i], tm, i);
					y += RowH;
				}
			}

			y += 6f;
			Widgets.DrawLineHorizontal(0f, y, width);
			y += 8f;
		}

		private static void DrawPawnRow(Rect r, Pawn p, TournamentManager tm, int index)
		{
			if (index % 2 == 1) Widgets.DrawAltRect(r);
			if (p == null) return;
			if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

			DrawPortrait(new Rect(r.x + 2f, r.y + 1f, r.height - 2f, r.height - 2f), p);

			var winRect = new Rect(r.xMax - 56f, r.y, 54f, r.height);
			Text.Anchor = TextAnchor.MiddleRight;
			GUI.color = MutedColor;
			Widgets.Label(winRect, "RimJoust.BoardWins".Translate(tm.WinsOf(p)));
			GUI.color = Color.white;

			var nameRect = new Rect(r.x + r.height + 6f, r.y, winRect.x - (r.x + r.height) - 10f, r.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			if (p.Dead) GUI.color = EliminatedColor;
			Widgets.Label(nameRect, NameOf(p));
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;

			LocateOnClick(r, p);
		}

		private struct MatchCell
		{
			public Pawn top, bottom, winner;
			public bool bye, inProgress, pending;
		}

		private List<List<MatchCell>> _bracketCache;
		private int _bracketMaxRound;
		private int _bracketSig = int.MinValue;

		private List<List<MatchCell>> BuildBracketCached(TournamentManager tm, out int maxRound)
		{
			var sig = tm.History.Count;
			sig = sig * 31 + (tm.Active ? 1 : 0);
			sig = sig * 31 + tm.CurrentRound;
			sig = sig * 31 + (tm.CurrentBoutA?.thingIDNumber ?? 0);
			sig = sig * 31 + (tm.CurrentBoutB?.thingIDNumber ?? 0);
			sig = sig * 31 + tm.RemainingThisRound.Count;

			if (_bracketCache == null || sig != _bracketSig)
			{
				_bracketCache = BuildBracket(tm, out _bracketMaxRound);
				_bracketSig = sig;
			}
			maxRound = _bracketMaxRound;
			return _bracketCache;
		}

		private void DrawBracket(Rect rect, TournamentManager tm)
		{
			var cols = BuildBracketCached(tm, out var maxRound);
			if (maxRound == 0)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = MutedColor;
				Widgets.Label(rect, "RimJoust.BracketEmpty".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			DrawBracketLegend(new Rect(rect.x, rect.y, rect.width, 18f));
			rect.yMin += 22f;

			var maxRows = 1;
			foreach (var c in cols) if (c.Count > maxRows) maxRows = c.Count;

			const float boxW = 152f, boxH = 44f, colGap = 42f, slotH = 56f;
			var contentW = maxRound * (boxW + colGap);
			var contentH = Mathf.Max(rect.height - 4f, maxRows * slotH);
			var view = new Rect(0f, 0f, contentW, contentH);

			Widgets.BeginScrollView(rect, ref _bracketScroll, view);
			Text.Font = GameFont.Tiny;

			GUI.color = ConnectorColor;
			for (var r = 1; r < cols.Count; r++)
			{
				var prev = cols[r - 1];
				var cur = cols[r];
				for (var j = 0; j < prev.Count; j++)
				{
					var ti = j / 2;
					if (ti >= cur.Count) continue;
					var px = (r - 1) * (boxW + colGap) + boxW;
					var py = contentH * (j + 0.5f) / prev.Count;
					var cxr = r * (boxW + colGap);
					var cyr = contentH * (ti + 0.5f) / cur.Count;
					DrawConnector(px, py, cxr, cyr);
				}
			}
			GUI.color = Color.white;

			for (var r = 0; r < cols.Count; r++)
			{
				var col = cols[r];
				for (var i = 0; i < col.Count; i++)
				{
					var cy = contentH * (i + 0.5f) / col.Count;
					DrawMatchCell(new Rect(r * (boxW + colGap), cy - boxH / 2f, boxW, boxH), col[i]);
				}
			}

			Text.Font = GameFont.Small;
			Widgets.EndScrollView();
		}

		private static List<List<MatchCell>> BuildBracket(TournamentManager tm, out int maxRound)
		{
			var byRound = new Dictionary<int, List<MatchCell>>();
			List<MatchCell> Col(int r)
			{
				if (!byRound.TryGetValue(r, out var l)) { l = new List<MatchCell>(); byRound[r] = l; }
				return l;
			}

			foreach (var rec in tm.History)
				Col(rec.round).Add(new MatchCell { top = rec.a, bottom = rec.b, winner = rec.winner, bye = rec.bye });

			if (tm.Active)
			{
				var r = tm.CurrentRound;
				if (tm.CurrentBoutA != null || tm.CurrentBoutB != null)
					Col(r).Add(new MatchCell { top = tm.CurrentBoutA, bottom = tm.CurrentBoutB, inProgress = true });

				var pending = tm.RemainingThisRound;
				var i = 0;
				for (; i + 1 < pending.Count; i += 2)
					Col(r).Add(new MatchCell { top = pending[i], bottom = pending[i + 1], pending = true });
				if (i < pending.Count)
					Col(r).Add(new MatchCell { top = pending[i], bottom = null, bye = true, pending = true });
			}

			maxRound = 0;
			foreach (var k in byRound.Keys) if (k > maxRound) maxRound = k;

			var cols = new List<List<MatchCell>>();
			for (var r = 1; r <= maxRound; r++)
				cols.Add(byRound.TryGetValue(r, out var l) ? l : new List<MatchCell>());
			return cols;
		}

		private static void DrawBracketLegend(Rect row)
		{
			Text.Font = GameFont.Tiny;
			var x = row.x;
			x = LegendSwatch(row, x, AdvancedColor, "RimJoust.LegendAdvanced".Translate());
			x = LegendSwatch(row, x, ProgressColor, "RimJoust.LegendLive".Translate());
			LegendSwatch(row, x, EliminatedColor, "RimJoust.LegendEliminated".Translate());
			Text.Font = GameFont.Small;
		}

		private static float LegendSwatch(Rect row, float x, Color c, string label)
		{
			var box = new Rect(x, row.y + 3f, 11f, 11f);
			Widgets.DrawBoxSolid(box, c);
			var w = Text.CalcSize(label).x;
			var lr = new Rect(box.xMax + 4f, row.y, w + 2f, row.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(lr, label);
			Text.Anchor = TextAnchor.UpperLeft;
			return lr.xMax + 12f;
		}

		private static void DrawConnector(float x1, float y1, float x2, float y2)
		{
			var midX = (x1 + x2) / 2f;
			Widgets.DrawLineHorizontal(x1, y1, midX - x1);
			Widgets.DrawLineVertical(midX, Mathf.Min(y1, y2), Mathf.Abs(y2 - y1));
			Widgets.DrawLineHorizontal(midX, y2, x2 - midX);
		}

		private static void DrawMatchCell(Rect box, MatchCell m)
		{
			Widgets.DrawBoxSolid(box, CellBg);
			if (m.inProgress) { GUI.color = ProgressColor; Widgets.DrawBox(box); GUI.color = Color.white; }
			else Widgets.DrawBox(box);

			var resolved = !m.pending && !m.inProgress;
			var top = new Rect(box.x + 4f, box.y + 2f, box.width - 8f, box.height / 2f - 2f);
			var bot = new Rect(box.x + 4f, box.center.y, box.width - 8f, box.height / 2f - 2f);

			DrawCellName(top, m.top, resolved && m.winner == m.top);
			if (m.bye)
			{
				Text.Anchor = TextAnchor.MiddleLeft;
				GUI.color = MutedColor;
				Widgets.Label(bot, "RimJoust.BracketBye".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}
			else DrawCellName(bot, m.bottom, resolved && m.winner == m.bottom);
		}

		private static void DrawCellName(Rect rect, Pawn p, bool isWinner)
		{
			Text.Anchor = TextAnchor.MiddleLeft;
			if (p == null)
			{
				GUI.color = MutedColor;
				Widgets.Label(rect, "RimJoust.BoardNobody".Translate());
			}
			else
			{
				GUI.color = p.Dead ? EliminatedColor : (isWinner ? AdvancedColor : Color.white);
				Widgets.Label(rect, NameOf(p));
			}
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			LocateOnClick(rect, p);
		}

		private void DrawJournal(Rect rect, TournamentManager tm)
		{
			var log = tm.EventLog;
			if (log == null || log.Count == 0)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = MutedColor;
				Widgets.Label(rect, "RimJoust.JournalEmpty".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			var w = rect.width - 18f;
			var total = 0f;
			for (var i = 0; i < log.Count; i++)
				total += Mathf.Max(24f, Text.CalcHeight(log[i], w - 8f)) + 2f;

			var view = new Rect(0f, 0f, w, total);
			Widgets.BeginScrollView(rect, ref _journalScroll, view);

			var y = 0f;
			var alt = 0;
			for (var i = log.Count - 1; i >= 0; i--)
			{
				var rh = Mathf.Max(24f, Text.CalcHeight(log[i], w - 8f));
				var rr = new Rect(0f, y, w, rh);
				if (alt++ % 2 == 1) Widgets.DrawAltRect(rr);
				Widgets.Label(new Rect(rr.x + 4f, rr.y + 2f, rr.width - 8f, rr.height), log[i]);
				y += rh + 2f;
			}

			Widgets.EndScrollView();
		}

		private List<KeyValuePair<string, int>> _titleTally;
		private int _tallySig = -1;

		private List<KeyValuePair<string, int>> TitleTally(TournamentManager tm)
		{
			if (_titleTally == null || _tallySig != tm.PastTournaments.Count)
			{
				var counts = new Dictionary<string, int>();
				foreach (var rec in tm.PastTournaments)
				{
					if (rec == null || string.IsNullOrEmpty(rec.championName)) continue;
					var key = rec.championFaction != null
						? rec.championName + " (" + rec.championFaction + ")"
						: rec.championName;
					counts.TryGetValue(key, out var n);
					counts[key] = n + 1;
				}
				_titleTally = new List<KeyValuePair<string, int>>(counts);
				_titleTally.Sort((x, z) => z.Value.CompareTo(x.Value));
				_tallySig = tm.PastTournaments.Count;
			}
			return _titleTally;
		}

		private void DrawHall(Rect rect, TournamentManager tm)
		{
			var records = tm.PastTournaments;
			if (records == null || records.Count == 0)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = MutedColor;
				Widgets.Label(rect, "RimJoust.HallEmpty".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				return;
			}

			var view = new Rect(0f, 0f, rect.width - 18f, _hallHeight);
			Widgets.BeginScrollView(rect, ref _hallScroll, view);
			var y = 0f;

			var tally = TitleTally(tm);
			GUI.color = HeaderColor;
			Widgets.Label(new Rect(0f, y, view.width, 22f), "RimJoust.HallTitles".Translate());
			GUI.color = Color.white;
			y += 24f;
			for (var i = 0; i < tally.Count; i++)
			{
				var row = new Rect(0f, y, view.width, 24f);
				if (i % 2 == 1) Widgets.DrawAltRect(row);
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 90f, row.height), tally[i].Key);
				Text.Anchor = TextAnchor.MiddleRight;
				GUI.color = TitleColor;
				Widgets.Label(new Rect(row.xMax - 86f, row.y, 82f, row.height), "RimJoust.HallTitleCount".Translate(tally[i].Value));
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				y += 24f;
			}

			y += 6f;
			Widgets.DrawLineHorizontal(0f, y, view.width);
			y += 8f;

			GUI.color = HeaderColor;
			Widgets.Label(new Rect(0f, y, view.width, 22f), "RimJoust.HallHistory".Translate(records.Count));
			GUI.color = Color.white;
			y += 24f;

			const float rowH = 46f;
			var alt = 0;
			for (var i = records.Count - 1; i >= 0; i--)
			{
				var rec = records[i];
				if (rec == null) continue;
				var row = new Rect(0f, y, view.width, rowH);
				if (alt++ % 2 == 1) Widgets.DrawAltRect(row);

				var name = rec.championFaction != null
					? rec.championName + " (" + rec.championFaction + ")"
					: rec.championName;
				GUI.color = TitleColor;
				Widgets.Label(new Rect(row.x + 4f, row.y + 3f, row.width * 0.60f, 20f), name);
				GUI.color = MutedColor;
				Text.Anchor = TextAnchor.UpperRight;
				Widgets.Label(new Rect(row.x + row.width * 0.60f, row.y + 3f, row.width * 0.40f - 4f, 20f), rec.date ?? "");
				Text.Anchor = TextAnchor.UpperLeft;

				var meta = "RimJoust.HallMeta".Translate(rec.participants, rec.rounds).ToString();
				if (rec.kind == "grand")
					meta = "RimJoust.HallGrandTag".Translate().ToString() + "  -  " + meta;
				var line2 = rec.queenName != null
					? "RimJoust.HallQueen".Translate(rec.queenName).ToString() + "  -  " + meta
					: meta;
				Widgets.Label(new Rect(row.x + 4f, row.y + 23f, row.width - 8f, 20f), line2);
				GUI.color = Color.white;
				y += rowH;
			}

			_hallHeight = y + 4f;
			Widgets.EndScrollView();
		}

		private static string NameOf(Pawn p)
		{
			if (p == null) return "RimJoust.BoardNobody".Translate();
			return p.Dead ? p.LabelShortCap + " " + "RimJoust.DeadTag".Translate() : p.LabelShortCap.ToString();
		}

		private static void DrawPortrait(Rect rect, Pawn p)
		{
			Widgets.DrawBoxSolid(rect, PortraitBg);
			if (p == null || p.Dead || !p.Spawned) return;
			var tex = PortraitsCache.Get(p, new Vector2(rect.width, rect.height), Rot4.South, default, 1f);
			GUI.DrawTexture(rect, tex);
		}

		private static void LocateOnClick(Rect rect, Pawn p)
		{
			if (p == null) return;
			TooltipHandler.TipRegion(rect, new TipSignal("RimJoust.LocateTip".Translate(), p.thingIDNumber ^ 0x10CA7E));
			if (Widgets.ButtonInvisible(rect)) CameraJumper.TryJumpAndSelect(p);
		}
	}
}
