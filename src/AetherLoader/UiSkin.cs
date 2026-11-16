using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// The window's look: a quiet neutral dark with one blue accent, rounded buttons, fields, rows and cards. Text meets
	/// WCAG AA (4.5:1) on its background (contrasts beside the colours below). States are spelled
	/// out (ok / off / skip / fail), never shown by colour alone. Segoe UI for the interface, Consolas for ids, versions
	/// and paths. Sizes are built for the UI scale (fonts rasterised at their final size: no blurry scaling).
	/// </summary>
	internal sealed class UiSkin {
		// a quiet neutral dark with one blue accent (contrast against the window #1E1F22 in brackets)
		public static readonly Color Background = Hex(0x1E1F22);  // the window, the pages
		public static readonly Color Panel = Hex(0x26282C);       // the side bar, cards
		public static readonly Color Rail = Hex(0x222326);        // the activity bar, the status bar
		public static readonly Color Line = Hex(0x34363B);        // dividers, card edges
		public static readonly Color Hover = Hex(0x2F3237);
		public static readonly Color Chosen = Hex(0x26395C);      // a chosen row
		public static readonly Color Accent = Hex(0x2F6BE0);      // white on it 5.0:1
		public static readonly Color Foreground = Hex(0xDFE1E5);  // 13.0:1
		public static readonly Color Secondary = Hex(0x9DA0A8);   // 6.6:1 (5.9:1 on Panel)
		public static readonly Color Heading = Hex(0x9DA0A8);
		public static readonly Color Bright = Hex(0xF2F3F5);
		public static readonly Color Link = Hex(0x6EA0FF);        // 7.0:1
		public static readonly Color Ok = Hex(0x6CC070);          // 8.4:1
		public static readonly Color Off = Hex(0x9DA0A8);
		public static readonly Color Warning = Hex(0xE0B341);     // 9.6:1
		public static readonly Color Error = Hex(0xF07B74);       // 6.9:1
		public float Scale;
		public Font Ui, Mono;
		public GUIStyle Window, TitleBar, Title, Version, Sidebar, Editor, Divider, Header, Row, RowSelected, RowFocus, Cell, CellMono,
			CellRight, Check, CheckOn, Name, Id, Key, Value, ValueMono, Section, Text, TextMono, ErrorBox, Notice, Button, ButtonPrimary,
			Tab, TabActive, Input, Placeholder, StatusBar, StatusItem, StatusText;
		public Texture2D ScrollTrack, ScrollThumb, Tick, Cross, Maximize, Restore, Collapse, Expand, Sash, ChevronDown, ChevronRight, ModsIcon, SettingsIcon, SwitchOn, SwitchOff;
		public GUIStyle ActivityBar, Activity, ActivityActive, Breadcrumb, Switch_, SliderTrack, SliderThumb, TreeRow, TreeRowSelected, TreeChild, TreeChildOn, FieldLabel, Small, Disc, Card;

		private UiSkin() {
		}

		public int S(float px) => Mathf.RoundToInt(px * Scale);

		/// <summary>The text size (1 normal, larger for Large text): fonts and the heights of what holds a line of text.</summary>
		public float TextScale = 1f;

		/// <summary>A size that grows with the text (fonts, row heights, label widths).</summary>
		public int T(float px) => Mathf.RoundToInt(px * Scale * TextScale);

		public static UiSkin Create(float scale, float text = 1f) {
			var s = new UiSkin { Scale = scale, TextScale = text };
			s.Ui = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Segoe UI Symbol", "Arial" }, s.S(13));
			s.Mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, s.S(13));
			s.ScrollTrack = Solid(new Color(0, 0, 0, 0));
			s.ScrollThumb = Solid(Hex(0x8A8D94, 0.35f));

			// drawn icons (symbol glyphs are not in Segoe UI and IMGUI does not fall back to another font)
			int icon = s.S(16);
			s.Tick = Stroke(icon, Color.white, 2.0f * scale, new Vector2(0.22f, 0.52f), new Vector2(0.42f, 0.72f), new Vector2(0.80f, 0.30f));
			s.Cross = Stroke(icon, Foreground, 1.4f * scale, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
			Stroke(s.Cross, Foreground, 1.4f * scale, new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f));
			s.Maximize = Stroke(icon, Foreground, 1.2f * scale, new Vector2(0.27f, 0.27f), new Vector2(0.73f, 0.27f), new Vector2(0.73f, 0.73f), new Vector2(0.27f, 0.73f), new Vector2(0.27f, 0.27f));
			// restore: a square with another behind it (top right)
			s.Restore = Stroke(icon, Foreground, 1.2f * scale, new Vector2(0.25f, 0.25f), new Vector2(0.63f, 0.25f), new Vector2(0.63f, 0.63f), new Vector2(0.25f, 0.63f), new Vector2(0.25f, 0.25f));
			Stroke(s.Restore, Foreground, 1.2f * scale, new Vector2(0.37f, 0.63f), new Vector2(0.37f, 0.75f), new Vector2(0.75f, 0.75f), new Vector2(0.75f, 0.37f), new Vector2(0.63f, 0.37f));
			s.Collapse = Stroke(icon, Foreground, 1.4f * scale, new Vector2(0.60f, 0.22f), new Vector2(0.34f, 0.50f), new Vector2(0.60f, 0.78f));
			s.Expand = Stroke(icon, Foreground, 1.4f * scale, new Vector2(0.40f, 0.22f), new Vector2(0.66f, 0.50f), new Vector2(0.40f, 0.78f));
			s.Sash = Solid(Accent);
			s.ChevronDown = Stroke(icon, Secondary, 1.4f * scale, new Vector2(0.25f, 0.40f), new Vector2(0.50f, 0.65f), new Vector2(0.75f, 0.40f));
			s.ChevronRight = Stroke(icon, Secondary, 1.4f * scale, new Vector2(0.40f, 0.25f), new Vector2(0.65f, 0.50f), new Vector2(0.40f, 0.75f));
			// the activity bar's icons (24 px): mods (four blocks), settings (three sliders)
			int big = s.S(24);
			s.ModsIcon = Stroke(big, Bright, 1.5f * scale, new Vector2(0.18f, 0.18f), new Vector2(0.44f, 0.18f), new Vector2(0.44f, 0.44f), new Vector2(0.18f, 0.44f), new Vector2(0.18f, 0.18f));
			Stroke(s.ModsIcon, Bright, 1.5f * scale, new Vector2(0.56f, 0.18f), new Vector2(0.82f, 0.18f), new Vector2(0.82f, 0.44f), new Vector2(0.56f, 0.44f), new Vector2(0.56f, 0.18f));
			Stroke(s.ModsIcon, Bright, 1.5f * scale, new Vector2(0.18f, 0.56f), new Vector2(0.44f, 0.56f), new Vector2(0.44f, 0.82f), new Vector2(0.18f, 0.82f), new Vector2(0.18f, 0.56f));
			Stroke(s.ModsIcon, Bright, 1.5f * scale, new Vector2(0.56f, 0.56f), new Vector2(0.82f, 0.56f), new Vector2(0.82f, 0.82f), new Vector2(0.56f, 0.82f), new Vector2(0.56f, 0.56f));
			s.SettingsIcon = Stroke(big, Bright, 1.5f * scale, new Vector2(0.16f, 0.25f), new Vector2(0.84f, 0.25f));
			Stroke(s.SettingsIcon, Bright, 1.5f * scale, new Vector2(0.16f, 0.50f), new Vector2(0.84f, 0.50f));
			Stroke(s.SettingsIcon, Bright, 1.5f * scale, new Vector2(0.16f, 0.75f), new Vector2(0.84f, 0.75f));
			Stroke(s.SettingsIcon, Bright, 3.2f * scale, new Vector2(0.62f, 0.18f), new Vector2(0.62f, 0.32f));
			Stroke(s.SettingsIcon, Bright, 3.2f * scale, new Vector2(0.34f, 0.43f), new Vector2(0.34f, 0.57f));
			Stroke(s.SettingsIcon, Bright, 3.2f * scale, new Vector2(0.70f, 0.68f), new Vector2(0.70f, 0.82f));
			s.SwitchOn = Switch(s.S(30), s.S(16), Accent, Color.white, true);
			s.SwitchOff = Switch(s.S(30), s.S(16), Hex(0x45484E), Hex(0xD0D2D6), false);

			s.Window = Rounded(s, 8, Background, Hex(0x3C3F45));
			s.TitleBar = new GUIStyle { padding = Pad(s, 14, 8, 0, 0) };
			s.Sidebar = new GUIStyle { normal = { background = Solid(Panel) } };
			s.Editor = new GUIStyle();
			s.Divider = new GUIStyle { fixedHeight = 1, normal = { background = Solid(Line) } };

			s.Title = Label(s, s.Ui, 13, Bright, FontStyle.Bold);
			s.Version = Label(s, s.Ui, 12, Secondary);
			s.Header = Label(s, s.Ui, 13, Bright, FontStyle.Bold);
			s.Header.padding = Pad(s, 14, 8, 0, 0);
			s.Header.alignment = TextAnchor.MiddleLeft;

			// rows: inset, rounded when hovered or chosen
			s.Row = new GUIStyle { fixedHeight = s.T(28), padding = Pad(s, 2, 6, 0, 0), margin = Pad(s, 6, 6, 1, 1), border = Edge(s, 5) };
			s.Row.hover.background = RoundTex(s, 5, Hover, Hover);
			s.RowSelected = new GUIStyle(s.Row);
			s.RowSelected.normal.background = s.RowSelected.hover.background = RoundTex(s, 5, Chosen, Chosen);
			s.RowFocus = new GUIStyle(s.RowSelected);
			s.RowFocus.normal.background = s.RowFocus.hover.background = RoundTex(s, 5, Chosen, Accent); // keyboard focus outline
			s.Cell = Label(s, s.Ui, 13, Foreground);
			s.Cell.fixedHeight = s.T(28);
			s.Cell.alignment = TextAnchor.MiddleLeft;
			s.Cell.clipping = TextClipping.Clip;
			s.Cell.padding = Pad(s, 6, 6, 0, 0);
			s.CellMono = new GUIStyle(s.Cell) { font = s.Mono, fontSize = s.T(12) };
			s.CellRight = new GUIStyle(s.CellMono) { alignment = TextAnchor.MiddleRight };
			s.CellRight.normal.textColor = Secondary;

			// checkbox: a rounded square, blue with a white tick when on
			s.Check = new GUIStyle { fixedWidth = s.S(16), fixedHeight = s.S(16), margin = Pad(s, 8, 6, 6, 6), border = Edge(s, 3),
				alignment = TextAnchor.MiddleCenter, font = s.Ui, fontSize = s.T(12), fontStyle = FontStyle.Bold };
			s.Check.normal.background = RoundTex(s, 3, Hex(0x1B1C1F), Hex(0x55585F));
			s.Check.hover.background = RoundTex(s, 3, Hex(0x1B1C1F), Accent);
			s.CheckOn = new GUIStyle(s.Check);
			s.CheckOn.normal.background = RoundTex(s, 3, Accent, Accent);
			s.CheckOn.hover.background = RoundTex(s, 3, Hex(0x3A78EE), Hex(0x3A78EE));
			s.CheckOn.imagePosition = ImagePosition.ImageOnly;
			s.CheckOn.padding = Pad(s, 1, 1, 1, 1);

			s.Name = Label(s, s.Ui, 20, Bright, FontStyle.Bold);
			s.Name.padding = Pad(s, 24, 24, 20, 0);
			s.Id = Label(s, s.Mono, 12, Secondary);
			s.Id.padding = Pad(s, 24, 24, 4, 12);
			s.Key = Label(s, s.Ui, 13, Secondary);
			s.Key.padding = Pad(s, 14, 0, 0, 0);
			s.Key.fixedHeight = s.T(28);
			s.Key.alignment = TextAnchor.MiddleLeft;
			s.Value = Label(s, s.Ui, 13, Foreground);
			s.Value.wordWrap = true;
			s.Value.padding = Pad(s, 0, 14, 6, 6);
			s.ValueMono = new GUIStyle(s.Value) { font = s.Mono, fontSize = s.T(12), wordWrap = false, clipping = TextClipping.Clip, fixedHeight = s.T(28), alignment = TextAnchor.MiddleLeft, padding = Pad(s, 0, 14, 0, 0) };
			s.Section = Label(s, s.Ui, 12, Secondary, FontStyle.Bold);
			s.Section.padding = Pad(s, 24, 24, 18, 6);
			s.Text = Label(s, s.Ui, 13, Foreground);
			s.Text.wordWrap = true;
			s.Text.padding = Pad(s, 24, 24, 2, 2);
			s.TextMono = new GUIStyle(s.Text) { font = s.Mono, fontSize = s.T(12) };
			s.Card = new GUIStyle { border = Edge(s, 6), margin = Pad(s, 24, 24, 0, 6), padding = Pad(s, 0, 0, 6, 6) };
			s.Card.normal.background = RoundTex(s, 6, Panel, Line);
			s.ErrorBox = new GUIStyle(s.Text) { border = Edge(s, 6), margin = Pad(s, 24, 24, 6, 6), padding = Pad(s, 12, 12, 8, 8) };
			s.ErrorBox.normal.background = RoundTex(s, 6, Hex(0x3A2326), Hex(0x7A3A3D));
			s.Notice = new GUIStyle(s.ErrorBox);
			s.Notice.normal.background = RoundTex(s, 6, Hex(0x372F1E), Hex(0x7A6430));

			s.Button = Label(s, s.Ui, 13, Foreground);
			s.Button.alignment = TextAnchor.MiddleCenter;
			s.Button.padding = Pad(s, 14, 14, 6, 7);
			s.Button.margin = Pad(s, 0, 8, 0, 0);
			s.Button.border = Edge(s, 5);
			s.Button.normal.background = RoundTex(s, 5, Hex(0x2F3237), Hex(0x43464C));
			s.Button.hover.background = RoundTex(s, 5, Hex(0x383B41), Hex(0x50545B));
			s.Button.active.background = RoundTex(s, 5, Hex(0x2A2D31), Hex(0x43464C));
			s.Button.hover.textColor = s.Button.active.textColor = Bright;
			s.ButtonPrimary = new GUIStyle(s.Button);
			s.ButtonPrimary.normal.background = RoundTex(s, 5, Accent, Accent);
			s.ButtonPrimary.hover.background = RoundTex(s, 5, Hex(0x3A78EE), Hex(0x3A78EE));
			s.ButtonPrimary.active.background = RoundTex(s, 5, Hex(0x2860CC), Hex(0x2860CC));
			s.ButtonPrimary.normal.textColor = s.ButtonPrimary.hover.textColor = s.ButtonPrimary.active.textColor = Color.white;

			s.Tab = Label(s, s.Ui, 12, Secondary, FontStyle.Bold);
			s.Tab.padding = Pad(s, 10, 10, 7, 7);
			s.Tab.hover.textColor = Bright;
			s.TabActive = new GUIStyle(s.Tab) { border = new RectOffset(0, 0, 0, 2) };
			s.TabActive.normal.textColor = s.TabActive.hover.textColor = Bright;
			s.TabActive.normal.background = Underlined(Accent);

			s.Input = new GUIStyle(GUI.skin.textField) { font = s.Ui, fontSize = s.T(13), padding = Pad(s, 9, 9, 6, 6), margin = new RectOffset(), border = Edge(s, 5) };
			s.Input.normal.background = s.Input.hover.background = RoundTex(s, 5, Hex(0x1B1C1F), Hex(0x43464C));
			s.Input.focused.background = RoundTex(s, 5, Hex(0x1B1C1F), Accent);
			s.Input.normal.textColor = s.Input.hover.textColor = s.Input.focused.textColor = Foreground;
			s.Placeholder = Label(s, s.Ui, 13, Hex(0x8A8D94));
			s.Placeholder.padding = s.Input.padding;

			s.StatusBar = new GUIStyle { padding = Pad(s, 8, 8, 0, 0), fixedHeight = s.T(26) };
			s.StatusText = Label(s, s.Ui, 12, Secondary);
			s.StatusText.alignment = TextAnchor.MiddleLeft;
			s.StatusText.fixedHeight = s.T(26);
			s.StatusText.padding = Pad(s, 6, 6, 0, 0);
			s.StatusItem = new GUIStyle(s.StatusText) { border = Edge(s, 4) };
			s.StatusItem.hover.background = RoundTex(s, 4, Hover, Hover);
			s.StatusItem.active.background = RoundTex(s, 4, Hex(0x2A2D31), Hex(0x2A2D31));
			s.StatusItem.hover.textColor = s.StatusItem.active.textColor = Bright;

			// the activity bar: the views on the far left, an icon each, the chosen one marked on its left in the accent
			s.ActivityBar = new GUIStyle { normal = { background = Solid(Rail) }, padding = Pad(s, 0, 0, 6, 0) };
			s.Activity = new GUIStyle { fixedWidth = s.S(44), fixedHeight = s.S(44), alignment = TextAnchor.MiddleCenter, imagePosition = ImagePosition.ImageOnly,
				padding = Pad(s, 10, 10, 10, 10), margin = new RectOffset() };
			s.Activity.normal.textColor = Secondary;
			s.ActivityActive = new GUIStyle(s.Activity) { border = new RectOffset(2, 0, 0, 0) };
			s.ActivityActive.normal.background = LeftLine(Accent);
			s.Breadcrumb = Label(s, s.Ui, 13, Foreground);
			s.Breadcrumb.alignment = TextAnchor.MiddleLeft;
			s.Breadcrumb.fixedHeight = s.T(40);
			s.Breadcrumb.padding = Pad(s, 24, 8, 0, 0);
			s.Switch_ = new GUIStyle { fixedWidth = s.S(30), fixedHeight = s.S(16), margin = Pad(s, 0, 8, 4, 4), imagePosition = ImagePosition.ImageOnly };
			s.SliderTrack = new GUIStyle { fixedHeight = s.S(4), margin = Pad(s, 0, 12, 12, 12), border = new RectOffset(0, 0, 0, 0) };
			s.SliderTrack.normal.background = Solid(Hex(0x3A3D42));
			s.SliderThumb = new GUIStyle { fixedWidth = s.S(14), fixedHeight = s.S(14), margin = new RectOffset(0, 0, s.S(-5), 0) };
			s.SliderThumb.normal.background = s.SliderThumb.active.background = Dot(s.S(14), Accent);
			s.SliderThumb.hover.background = Dot(s.S(14), Hex(0x3A78EE));
			// the mod tree: a mod's row, and its pages one step in (rounded when hovered or chosen, like the rows)
			s.TreeRow = new GUIStyle(s.Row);
			s.TreeRowSelected = new GUIStyle(s.RowSelected);
			s.TreeChild = new GUIStyle(s.Cell) { padding = Pad(s, 32, 6, 0, 0), margin = Pad(s, 6, 6, 1, 1), border = Edge(s, 5) };
			s.TreeChild.hover.background = s.Row.hover.background;
			s.TreeChild.hover.textColor = Bright;
			s.TreeChildOn = new GUIStyle(s.TreeChild);
			s.TreeChildOn.normal.background = s.TreeChildOn.hover.background = s.RowSelected.normal.background;
			s.TreeChildOn.normal.textColor = s.TreeChildOn.hover.textColor = Bright;
			s.FieldLabel = Label(s, s.Ui, 13, Foreground);
			s.FieldLabel.alignment = TextAnchor.MiddleLeft;
			s.FieldLabel.fixedHeight = s.T(34);
			s.FieldLabel.padding = Pad(s, 14, 8, 0, 0);
			s.Small = Label(s, s.Ui, 12, Secondary);
			s.Small.wordWrap = true;
			s.Small.padding = Pad(s, 24, 24, 2, 8);
			s.Disc = Label(s, s.Ui, 10, Foreground);
			s.Disc.alignment = TextAnchor.MiddleCenter;
			s.Disc.fixedHeight = s.T(28);
			s.Disc.fixedWidth = s.S(18);			return s;
		}

		/// <summary>The styles a mod's panel draws with (Aether.API ModUi).</summary>
		public Aether.ModUi ToModUi(System.Action close) {
			var secondary = new GUIStyle(Text) { padding = Pad(this, 0, 0, 2, 2) };
			secondary.normal.textColor = secondary.hover.textColor = Secondary;
			var heading = new GUIStyle(Section) { padding = Pad(this, 0, 0, 12, 4) };
			var text = new GUIStyle(Text) { padding = Pad(this, 0, 0, 2, 2) };
			var mono = new GUIStyle(TextMono) { padding = Pad(this, 0, 0, 2, 2) };
			var errorBox = new GUIStyle(ErrorBox) { margin = Pad(this, 0, 0, 6, 4) };
			var notice = new GUIStyle(Notice) { margin = Pad(this, 0, 0, 6, 4) };
			// a page sets its rows' and cells' heights itself (GUILayout.Height): no fixed height of ours that would win over
			// it and let rows overlap; buttons keep a little room above and below, so stacked ones stay apart
			var row = new GUIStyle(Row) { fixedHeight = 0 };
			var rowSelected = new GUIStyle(RowSelected) { fixedHeight = 0 };
			var cell = new GUIStyle(Cell) { fixedHeight = 0 };
			var cellMono = new GUIStyle(CellMono) { fixedHeight = 0 };
			var button = new GUIStyle(Button) { margin = Pad(this, 0, 8, 2, 2) };
			var buttonPrimary = new GUIStyle(ButtonPrimary) { margin = Pad(this, 0, 8, 2, 2) };
			return new Aether.ModUi {
				// a page's sizes (ui.S) grow with the text too: its rows and columns hold text
				Scale = Scale * TextScale, Text = text, TextMono = mono, Secondary = secondary, Heading = heading, Button = button, ButtonPrimary = buttonPrimary,
				Input = Input, Row = row, RowSelected = rowSelected, Cell = cell, CellMono = cellMono, ErrorBox = errorBox, Notice = notice,
				Tab = Tab, TabActive = TabActive, Divider = Divider,
				OkColor = "#" + Html(Ok), WarningColor = "#" + Html(Warning), ErrorColor = "#" + Html(Error), SecondaryColor = "#" + Html(Secondary),
				CloseAction = close,
			};
		}

		private static GUIStyle Label(UiSkin s, Font font, int size, Color color, FontStyle style = FontStyle.Normal) {
			var g = new GUIStyle { font = font, fontSize = s.T(size), fontStyle = style, richText = true, margin = new RectOffset(), padding = new RectOffset() };
			g.normal.textColor = color;
			g.hover.textColor = color;
			g.active.textColor = color;
			return g;
		}

		private static GUIStyle Box(Color fill, Color frame) {
			var g = new GUIStyle { border = new RectOffset(1, 1, 1, 1), padding = new RectOffset(1, 1, 1, 1) };
			g.normal.background = g.onNormal.background = Framed(fill, frame);
			return g;
		}

		private static RectOffset Pad(UiSkin s, int left, int right, int top, int bottom) => new RectOffset(s.S(left), s.S(right), s.S(top), s.S(bottom));

		private static Texture2D Solid(Color c) {
			var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
			t.SetPixel(0, 0, c);
			t.Apply();
			return t;
		}

		// 3 x 3: a 1 px frame around the fill (drawn nine-sliced with border 1)
		private static Texture2D Framed(Color fill, Color frame) {
			var t = new Texture2D(3, 3, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			for (int y = 0; y < 3; ++y) {
				for (int x = 0; x < 3; ++x) {
					t.SetPixel(x, y, x == 1 && y == 1 ? fill : frame);
				}
			}
			t.Apply();
			return t;
		}

		// an anti-aliased polyline on a transparent square (points in 0..1, y down)
		private static Texture2D Stroke(int size, Color color, float width, params Vector2[] points) {
			var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			var clear = new Color[size * size];
			t.SetPixels(clear);
			Stroke(t, color, width, points);
			return t;
		}

		private static void Stroke(Texture2D t, Color color, float width, params Vector2[] points) {
			int size = t.width;
			for (int y = 0; y < size; ++y) {
				for (int x = 0; x < size; ++x) {
					var p = new Vector2(x + 0.5f, size - y - 0.5f); // texture y is up
					float d = float.MaxValue;
					for (int i = 0; i + 1 < points.Length; ++i) {
						Vector2 a = points[i] * size, b = points[i + 1] * size;
						Vector2 ab = b - a;
						float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
						d = Mathf.Min(d, Vector2.Distance(p, a + ab * k));
					}
					float alpha = Mathf.Clamp01(width / 2 + 0.5f - d);
					if (alpha > 0) {
						Color c = color;
						c.a = Mathf.Max(t.GetPixel(x, y).a, alpha);
						t.SetPixel(x, y, c);
					}
				}
			}
			t.Apply();
		}

		/// <summary>A box style with a rounded, framed background (nine-sliced).</summary>
		private static GUIStyle Rounded(UiSkin s, int radius, Color fill, Color frame) {
			var g = new GUIStyle { border = Edge(s, radius), padding = new RectOffset(1, 1, 1, 1) };
			g.normal.background = g.onNormal.background = RoundTex(s, radius, fill, frame);
			return g;
		}

		private static RectOffset Edge(UiSkin s, int radius) {
			int e = s.S(radius) + 1;
			return new RectOffset(e, e, e, e);
		}

		// a rounded rectangle (radius in 1440p pixels), filled, with a 1 px frame; drawn nine-sliced with Edge(radius)
		private static Texture2D RoundTex(UiSkin s, int radius, Color fill, Color frame) {
			int r = s.S(radius), size = r * 2 + 3;
			var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			for (int y = 0; y < size; ++y) {
				for (int x = 0; x < size; ++x) {
					// distance outside the rounded rectangle's edge (negative inside)
					float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
					float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
					float outer = Mathf.Clamp01(0.5f - d);
					float inner = Mathf.Clamp01(0.5f - (d + 1f));
					Color c = Color.Lerp(frame, fill, inner);
					c.a *= outer;
					t.SetPixel(x, y, c);
				}
			}
			t.Apply();
			return t;
		}

		// a 2 px line on the left (drawn nine-sliced with border left 2)
		private static Texture2D LeftLine(Color line) {
			var t = new Texture2D(3, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			t.SetPixel(0, 0, line);
			t.SetPixel(1, 0, line);
			t.SetPixel(2, 0, new Color(0, 0, 0, 0));
			t.Apply();
			return t;
		}

		// a filled anti-aliased circle
		private static Texture2D Dot(int size, Color color) {
			var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			float r = size / 2f;
			for (int y = 0; y < size; ++y) {
				for (int x = 0; x < size; ++x) {
					float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
					Color c = color;
					c.a = Mathf.Clamp01(r - d);
					t.SetPixel(x, y, c);
				}
			}
			t.Apply();
			return t;
		}

		// a switch: a rounded track with its knob on the right (on) or left (off)
		private static Texture2D Switch(int w, int h, Color track, Color knob, bool on) {
			var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			float r = h / 2f, kr = r - Mathf.Max(2f, h / 7f);
			var kc = new Vector2(on ? w - r : r, r);
			for (int y = 0; y < h; ++y) {
				for (int x = 0; x < w; ++x) {
					var p = new Vector2(x + 0.5f, y + 0.5f);
					var near = new Vector2(Mathf.Clamp(p.x, r, w - r), r);
					float a = Mathf.Clamp01(r - Vector2.Distance(p, near) + 0.5f);
					float k = Mathf.Clamp01(kr - Vector2.Distance(p, kc) + 0.5f);
					Color c = Color.Lerp(track, knob, k);
					c.a = a;
					t.SetPixel(x, y, c);
				}
			}
			t.Apply();
			return t;
		}

		// a 1 px line at the bottom (texture y = 0 is the bottom)
		private static Texture2D Underlined(Color line) {
			var t = new Texture2D(1, 3, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			t.SetPixel(0, 0, line);
			t.SetPixel(0, 1, new Color(0, 0, 0, 0));
			t.SetPixel(0, 2, new Color(0, 0, 0, 0));
			t.Apply();
			return t;
		}

		public static Color Hex(int rgb, float alpha = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

		public static string Html(Color c) => ColorUtility.ToHtmlStringRGB(c);
	}
}
