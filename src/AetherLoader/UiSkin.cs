using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// The window's look: VS Code's Dark+ colours. Text meets WCAG AA (4.5:1) on its background: foreground #CCCCCC and
	/// secondary #9D9D9D on #1E1E1E / #252526, white on the #007ACC status bar and the #0E639C button. States are spelled
	/// out (ok / off / skip / fail), never shown by colour alone. Segoe UI for the interface, Consolas for ids, versions
	/// and paths. Sizes are built for the UI scale (fonts rasterised at their final size: no blurry scaling).
	/// </summary>
	internal sealed class UiSkin {
		// Dark+ (contrast against #1E1E1E in brackets)
		public static readonly Color Foreground = Hex(0xCCCCCC);  // 10.8:1
		public static readonly Color Secondary = Hex(0x9D9D9D);   // 6.4:1
		public static readonly Color Heading = Hex(0xBBBBBB);     // 8.9:1
		public static readonly Color Bright = Hex(0xE7E7E7);
		public static readonly Color Link = Hex(0x3794FF);        // 5.6:1
		public static readonly Color Ok = Hex(0x89D185);          // 10.0:1
		public static readonly Color Off = Hex(0x9D9D9D);
		public static readonly Color Warning = Hex(0xCCA700);     // 7.7:1
		public static readonly Color Error = Hex(0xF48771);       // 7.2:1

		public float Scale;
		public Font Ui, Mono;
		public GUIStyle Window, TitleBar, Title, Version, Sidebar, Editor, Divider, Header, Row, RowSelected, RowFocus, Cell, CellMono,
			CellRight, Check, CheckOn, Name, Id, Key, Value, ValueMono, Section, Text, TextMono, ErrorBox, Notice, Button, ButtonPrimary,
			Tab, TabActive, Input, Placeholder, StatusBar, StatusItem, StatusText;
		public Texture2D ScrollTrack, ScrollThumb, Tick, Cross;

		private UiSkin() {
		}

		public int S(float px) => Mathf.RoundToInt(px * Scale);

		public static UiSkin Create(float scale) {
			var s = new UiSkin { Scale = scale };
			s.Ui = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Segoe UI Symbol", "Arial" }, s.S(13));
			s.Mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, s.S(13));
			s.ScrollTrack = Solid(new Color(0, 0, 0, 0));
			s.ScrollThumb = Solid(Hex(0x797979, 0.4f)); // Dark+ scrollbarSlider

			// drawn icons (symbol glyphs are not in Segoe UI and IMGUI does not fall back to another font)
			int icon = s.S(16);
			s.Tick = Stroke(icon, Color.white, 2.0f * scale, new Vector2(0.22f, 0.52f), new Vector2(0.42f, 0.72f), new Vector2(0.80f, 0.30f));
			s.Cross = Stroke(icon, Foreground, 1.4f * scale, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
			Stroke(s.Cross, Foreground, 1.4f * scale, new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f));

			s.Window = Box(Hex(0x1E1E1E), Hex(0x454545));
			s.TitleBar = Box(Hex(0x252526), Hex(0x2B2B2B));
			s.TitleBar.padding = Pad(s, 12, 12, 0, 0);
			s.Sidebar = Box(Hex(0x252526), Hex(0x2B2B2B));
			s.Editor = Box(Hex(0x1E1E1E), Hex(0x2B2B2B));
			s.Divider = new GUIStyle { fixedHeight = 1, normal = { background = Solid(Hex(0x2B2B2B)) } };

			s.Title = Label(s, s.Ui, 11, Heading, FontStyle.Bold);
			s.Version = Label(s, s.Ui, 12, Secondary);
			s.Header = Label(s, s.Ui, 11, Heading, FontStyle.Bold);
			s.Header.padding = Pad(s, 8, 8, 0, 0);
			s.Header.alignment = TextAnchor.MiddleLeft;

			s.Row = new GUIStyle { fixedHeight = s.S(22), padding = Pad(s, 0, 4, 0, 0), margin = new RectOffset() };
			s.Row.hover.background = Solid(Hex(0x2A2D2E));
			s.RowSelected = new GUIStyle(s.Row) { border = new RectOffset(1, 1, 1, 1) };
			s.RowSelected.normal.background = s.RowSelected.hover.background = Framed(Hex(0x04395E), Hex(0x04395E));
			s.RowFocus = new GUIStyle(s.RowSelected);
			s.RowFocus.normal.background = s.RowFocus.hover.background = Framed(Hex(0x04395E), Hex(0x007FD4)); // keyboard focus outline
			s.Cell = Label(s, s.Ui, 13, Foreground);
			s.Cell.fixedHeight = s.S(22);
			s.Cell.alignment = TextAnchor.MiddleLeft;
			s.Cell.clipping = TextClipping.Clip;
			s.Cell.padding = Pad(s, 6, 6, 0, 0);
			s.CellMono = new GUIStyle(s.Cell) { font = s.Mono, fontSize = s.S(12) };
			s.CellRight = new GUIStyle(s.CellMono) { alignment = TextAnchor.MiddleRight };
			s.CellRight.normal.textColor = Secondary;

			// checkbox: a framed square, blue with a white tick when on
			s.Check = new GUIStyle { fixedWidth = s.S(16), fixedHeight = s.S(16), margin = Pad(s, 8, 6, 3, 3), border = new RectOffset(1, 1, 1, 1),
				alignment = TextAnchor.MiddleCenter, font = s.Ui, fontSize = s.S(12), fontStyle = FontStyle.Bold };
			s.Check.normal.background = Framed(Hex(0x3C3C3C), Hex(0x6B6B6B));
			s.Check.hover.background = Framed(Hex(0x3C3C3C), Hex(0x007FD4));
			s.CheckOn = new GUIStyle(s.Check);
			s.CheckOn.normal.background = Framed(Hex(0x0E639C), Hex(0x0E639C));
			s.CheckOn.hover.background = Framed(Hex(0x1177BB), Hex(0x007FD4));
			s.CheckOn.imagePosition = ImagePosition.ImageOnly;
			s.CheckOn.padding = Pad(s, 1, 1, 1, 1);

			s.Name = Label(s, s.Ui, 16, Bright, FontStyle.Bold);
			s.Name.padding = Pad(s, 16, 16, 12, 0);
			s.Id = Label(s, s.Mono, 12, Secondary);
			s.Id.padding = Pad(s, 16, 16, 2, 8);
			s.Key = Label(s, s.Ui, 13, Secondary);
			s.Key.padding = Pad(s, 16, 0, 0, 0);
			s.Key.fixedHeight = s.S(22);
			s.Key.alignment = TextAnchor.MiddleLeft;
			s.Value = Label(s, s.Ui, 13, Foreground);
			s.Value.wordWrap = true;
			s.Value.padding = Pad(s, 0, 16, 2, 2);
			s.Value.padding = Pad(s, 0, 16, 3, 3);
			s.ValueMono = new GUIStyle(s.Value) { font = s.Mono, fontSize = s.S(12), wordWrap = false, clipping = TextClipping.Clip, fixedHeight = s.S(22), alignment = TextAnchor.MiddleLeft, padding = Pad(s, 0, 16, 0, 0) };
			s.Section = Label(s, s.Ui, 11, Heading, FontStyle.Bold);
			s.Section.padding = Pad(s, 16, 16, 14, 4);
			s.Text = Label(s, s.Ui, 13, Foreground);
			s.Text.wordWrap = true;
			s.Text.padding = Pad(s, 16, 16, 2, 2);
			s.TextMono = new GUIStyle(s.Text) { font = s.Mono, fontSize = s.S(12) };
			s.ErrorBox = new GUIStyle(s.Text) { border = new RectOffset(1, 1, 1, 1), margin = Pad(s, 16, 16, 6, 4), padding = Pad(s, 8, 8, 5, 5) };
			s.ErrorBox.normal.background = Framed(Hex(0x5A1D1D), Hex(0xBE1100));
			s.Notice = new GUIStyle(s.ErrorBox);
			s.Notice.normal.background = Framed(Hex(0x352A05), Hex(0xB89500));

			s.Button = Label(s, s.Ui, 13, Foreground);
			s.Button.alignment = TextAnchor.MiddleCenter;
			s.Button.padding = Pad(s, 12, 12, 4, 5);
			s.Button.margin = Pad(s, 0, 8, 0, 0);
			s.Button.normal.background = Solid(Hex(0x3A3D41));
			s.Button.hover.background = Solid(Hex(0x45494E));
			s.Button.active.background = Solid(Hex(0x313337));
			s.Button.hover.textColor = s.Button.active.textColor = Foreground;
			s.ButtonPrimary = new GUIStyle(s.Button);
			s.ButtonPrimary.normal.background = Solid(Hex(0x0E639C));
			s.ButtonPrimary.hover.background = Solid(Hex(0x1177BB));
			s.ButtonPrimary.active.background = Solid(Hex(0x0B5585));
			s.ButtonPrimary.normal.textColor = s.ButtonPrimary.hover.textColor = s.ButtonPrimary.active.textColor = Color.white;

			s.Tab = Label(s, s.Ui, 11, Secondary, FontStyle.Bold);
			s.Tab.padding = Pad(s, 8, 8, 6, 6);
			s.Tab.hover.textColor = Bright;
			s.TabActive = new GUIStyle(s.Tab) { border = new RectOffset(0, 0, 0, 1) };
			s.TabActive.normal.textColor = s.TabActive.hover.textColor = Bright;
			s.TabActive.normal.background = Underlined(Bright);

			s.Input = new GUIStyle(GUI.skin.textField) { font = s.Ui, fontSize = s.S(13), padding = Pad(s, 6, 6, 4, 4), margin = new RectOffset(), border = new RectOffset(1, 1, 1, 1) };
			s.Input.normal.background = s.Input.hover.background = Framed(Hex(0x3C3C3C), Hex(0x3C3C3C));
			s.Input.focused.background = Framed(Hex(0x3C3C3C), Hex(0x007FD4));
			s.Input.normal.textColor = s.Input.hover.textColor = s.Input.focused.textColor = Foreground;
			s.Placeholder = Label(s, s.Ui, 13, Hex(0xA6A6A6));
			s.Placeholder.padding = s.Input.padding;

			s.StatusBar = new GUIStyle { padding = Pad(s, 6, 6, 0, 0), fixedHeight = s.S(22) };
			s.StatusBar.normal.background = Solid(Hex(0x007ACC));
			s.StatusText = Label(s, s.Ui, 12, Color.white);
			s.StatusText.alignment = TextAnchor.MiddleLeft;
			s.StatusText.fixedHeight = s.S(22);
			s.StatusText.padding = Pad(s, 6, 6, 0, 0);
			s.StatusItem = new GUIStyle(s.StatusText);
			s.StatusItem.hover.background = Solid(Hex(0x1F8AD2));
			s.StatusItem.active.background = Solid(Hex(0x0065A9));
			s.StatusItem.hover.textColor = s.StatusItem.active.textColor = Color.white;
			return s;
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
			return new Aether.ModUi {
				Scale = Scale, Text = text, TextMono = mono, Secondary = secondary, Heading = heading, Button = Button, ButtonPrimary = ButtonPrimary,
				Input = Input, Row = Row, RowSelected = RowSelected, Cell = Cell, CellMono = CellMono, ErrorBox = errorBox, Notice = notice,
				Tab = Tab, TabActive = TabActive, Divider = Divider,
				OkColor = "#" + Html(Ok), WarningColor = "#" + Html(Warning), ErrorColor = "#" + Html(Error), SecondaryColor = "#" + Html(Secondary),
				CloseAction = close,
			};
		}

		private static GUIStyle Label(UiSkin s, Font font, int size, Color color, FontStyle style = FontStyle.Normal) {
			var g = new GUIStyle { font = font, fontSize = s.S(size), fontStyle = style, richText = true, margin = new RectOffset(), padding = new RectOffset() };
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
