using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aether;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// The mod manager (F9 by default, loader.json "hotkey"), laid out like VS Code:
	///   title bar     name, version; maximise / restore (or a double click), close
	///   activity bar  the views on the far left: Mods, Settings (clicking Mods again folds the side bar away)
	///   side bar      the mods as a tree: each mod, and under it its pages (Details, then the mod's own pages);
	///                 a filter field above; the sash on its right edge makes it wider or narrower
	///   editor        where one is ("Aether Parts › Adjust"), the mod's switch and folders, then the page
	///   status bar    counts, pending changes with undo, whether the game's input is held, mods folder, close
	/// Settings: the window's size (UI scale), opacity, fading while the pointer is away, holding the game's mouse and
	/// keys back (InputGate), the key that opens it.
	/// Keyboard: Up / Down select a mod, Space switches it, Ctrl+F filters, Esc leaves the filter or closes.
	/// Switches take effect at the next start. The window moves (title bar) and resizes (bottom right corner); its place is
	/// kept in loader.json in 1440p units. Fonts are rasterised at the final size.
	/// </summary>
	internal sealed class ManagerWindow : MonoBehaviour {
		private enum View { Mods, Settings }

		private const int WindowId = 0x41454C44;
		private const string FilterControl = "aether-loader-filter";
		private const float MinWidth = 640, MinHeight = 360, TitleHeight = 38, ActivityWidth = 44, MinList = 200, KeyWidth = 72;

		private ModLoader loader_;
		private LoaderConfig config_;
		private UiSkin skin_;
		private bool open_;
		private View view_;
		private string search_ = "";
		private string selected_; // folder of the selected mod
		private readonly Dictionary<string, int> page_ = new Dictionary<string, int>(); // the page shown per mod (0 = details)
		private readonly HashSet<string> expanded_ = new HashSet<string>();
		private bool keyboardFocus_;
		private Vector2 treeScroll_, detailScroll_, settingsScroll_;
		private float treeViewHeight_ = 300;
		private bool resizing_, sashDrag_;
		private Vector2 resizeDelta_;
		private bool cursorVisible_;
		private CursorLockMode cursorLock_;
		private string message_;
		private bool rebinding_;
		private float alpha_ = 1f;
		private bool hover_;
		private Rect windowPixels_;

		public void Init(ModLoader loader, LoaderConfig config) {
			loader_ = loader;
			config_ = config;
			alpha_ = config.Opacity;
			InputGate.Install(BepInEx.Logging.Logger.CreateLogSource("Aether Loader"));
			if (config.OpenAtStart) {
				Toggle();
			}
		}

		private void Update() {
			if (open_) {
				// the game hides and locks the cursor while driving
				Cursor.visible = true;
				Cursor.lockState = CursorLockMode.None;
			}
		}

		private void Toggle() {
			open_ = !open_;
			rebinding_ = false;
			if (open_) {
				cursorVisible_ = Cursor.visible;
				cursorLock_ = Cursor.lockState;
			} else {
				Cursor.visible = cursorVisible_;
				Cursor.lockState = cursorLock_;
				GUIUtility.keyboardControl = 0;
				InputGate.Set(false, false);
				Save(); // the window's place
			}
		}

		private float Scale => config_.UiScale > 0 ? config_.UiScale : AutoScale;
		private static float AutoScale => Mathf.Clamp(Screen.height / 1440f, 1f, 3f);

		private void OnGUI() {
			Event e = Event.current;
			if (rebinding_ && e.type == EventType.KeyDown && e.keyCode != KeyCode.None) {
				// the next key opens and closes the window (Esc keeps the old one)
				if (e.keyCode != KeyCode.Escape) {
					config_.Hotkey = e.keyCode;
					Save();
				}
				rebinding_ = false;
				e.Use();
				return;
			}
			// key events reach IMGUI with either of Unity's input handlers
			if (e.type == EventType.KeyDown && e.keyCode == config_.Hotkey) {
				Toggle();
				e.Use();
			}
			if (!open_) {
				return;
			}
			float scale = Scale;
			if (skin_ == null || !Mathf.Approximately(skin_.Scale, scale) || !Mathf.Approximately(skin_.TextScale, config_.TextSize)) {
				skin_ = UiSkin.Create(scale, config_.TextSize);
			}
			// the window in screen pixels (config keeps 1440p units); maximised, it fills the screen but for a margin
			Rect r;
			if (config_.Maximized) {
				float margin = skin_.S(16);
				r = new Rect(margin, margin, Screen.width - 2 * margin, Screen.height - 2 * margin);
			} else {
				Rect b = config_.Window;
				b.width = Mathf.Max(b.width, MinWidth);
				b.height = Mathf.Max(b.height, MinHeight);
				r = new Rect(b.x * scale, b.y * scale, b.width * scale, b.height * scale);
				r.width = Mathf.Min(r.width, Screen.width);
				r.height = Mathf.Min(r.height, Screen.height);
				r.x = Mathf.Clamp(r.x, 0, Screen.width - r.width);
				r.y = Mathf.Clamp(r.y, 0, Screen.height - r.height);
			}
			windowPixels_ = r;

			// what the window takes from the game this frame, and how see-through it is
			if (e.type == EventType.Repaint || e.isMouse || e.type == EventType.ScrollWheel) {
				hover_ = r.Contains(e.mousePosition);
			}
			bool dragging = GUIUtility.hotControl != 0 || resizing_ || sashDrag_;
			bool typing = GUIUtility.keyboardControl != 0 || rebinding_;
			InputGate.Set(config_.BlockMouse && (hover_ || dragging), config_.BlockKeys && (typing || hover_));
			if (e.type == EventType.Repaint) {
				float target = config_.FadeAway && !hover_ && !dragging && !typing ? Mathf.Min(config_.Opacity, config_.FadeOpacity) : config_.Opacity;
				alpha_ = Mathf.MoveTowards(alpha_, target, Time.unscaledDeltaTime * 3f);
			}
			Color was = GUI.color;
			GUI.color = new Color(1, 1, 1, alpha_);

			resizeDelta_ = Vector2.zero;
			bool maximized = config_.Maximized;
			r = GUI.Window(WindowId, r, DrawWindow, GUIContent.none, skin_.Window);
			if (!maximized && !config_.Maximized) {
				r.size += resizeDelta_;
				config_.Window = new Rect(r.x / scale, r.y / scale, r.width / scale, r.height / scale);
			}
			GUI.color = was;
		}

		private void DrawWindow(int id) {
			// a thin scrollbar: IMGUI finds a scrollbar's thumb by name in the current skin, so the skin's own styles are
			// changed for this window and put back after it
			GUIStyle bar = GUI.skin.verticalScrollbar, thumb = GUI.skin.verticalScrollbarThumb;
			Texture2D barBg = bar.normal.background, thumbBg = thumb.normal.background;
			float barWidth = bar.fixedWidth, thumbWidth = thumb.fixedWidth;
			RectOffset barMargin = bar.margin, barPadding = bar.padding;
			bar.normal.background = skin_.ScrollTrack;
			thumb.normal.background = skin_.ScrollThumb;
			bar.fixedWidth = thumb.fixedWidth = skin_.S(8);
			bar.margin = new RectOffset();
			bar.padding = new RectOffset();
			try {
				DrawContent();
			} finally {
				bar.normal.background = barBg;
				thumb.normal.background = thumbBg;
				bar.fixedWidth = barWidth;
				thumb.fixedWidth = thumbWidth;
				bar.margin = barMargin;
				bar.padding = barPadding;
			}
		}

		private void DrawContent() {
			Rect r = windowPixels_;
			List<ModInfo> mods = loader_.All.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
			List<ModInfo> shown = mods.Where(Matches).ToList();
			if (selected_ == null || !mods.Any(m => m.Folder == selected_)) {
				selected_ = (shown.FirstOrDefault() ?? mods.FirstOrDefault())?.Folder;
				if (selected_ != null) {
					expanded_.Add(selected_);
				}
			}
			HandleKeys(shown);
			float title = skin_.T(TitleHeight);
			float status = skin_.T(26);
			float body = r.height - 2 - title - status;
			float activity = skin_.S(ActivityWidth);
			bool side = view_ == View.Mods && !config_.ListCollapsed;
			float sideWidth = side ? Mathf.Round(Mathf.Clamp(config_.ListWidth * skin_.Scale, skin_.S(MinList), Mathf.Max(skin_.S(MinList), (r.width - activity) * 0.5f))) : 0;

			// a double click on the title bar maximises or restores (before the drag takes the click)
			Event ev = Event.current;
			if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2 && new Rect(0, 0, r.width - skin_.S(140), title).Contains(ev.mousePosition)) {
				ToggleMaximized();
				ev.Use();
			}
			GUILayout.BeginArea(new Rect(1, 1, r.width - 2, title));
			DrawTitleBar();
			GUILayout.EndArea();
			GUILayout.BeginArea(new Rect(1, 1 + title, activity, body), skin_.ActivityBar);
			DrawActivityBar();
			GUILayout.EndArea();
			if (side) {
				GUILayout.BeginArea(new Rect(1 + activity, 1 + title, sideWidth, body), skin_.Sidebar);
				DrawTree(mods, shown);
				GUILayout.EndArea();
			}
			float edX = 1 + activity + sideWidth;
			GUILayout.BeginArea(new Rect(edX, 1 + title, r.width - 1 - edX, body), skin_.Editor);
			if (view_ == View.Settings) {
				DrawSettings(mods);
			} else {
				DrawEditor(mods.FirstOrDefault(m => m.Folder == selected_));
			}
			GUILayout.EndArea();
			if (side) {
				DrawSash(new Rect(edX - skin_.S(3), 1 + title, skin_.S(6), body));
			}
			GUILayout.BeginArea(new Rect(1, r.height - 1 - status, r.width - 2, status), skin_.StatusBar);
			DrawStatusBar(mods);
			GUILayout.EndArea();
			// hairlines under the title bar and over the status bar
			if (Event.current.type == EventType.Repaint) {
				Texture line = skin_.Divider.normal.background;
				GUI.DrawTexture(new Rect(1, title, r.width - 2, 1), line);
				GUI.DrawTexture(new Rect(1, r.height - 1 - status, r.width - 2, 1), line);
				if (side) {
					GUI.DrawTexture(new Rect(edX - 1, 1 + title, 1, body), line);
				}
			}
			if (!config_.Maximized) {
				DrawResizeHandle(r);
				GUI.DragWindow(new Rect(0, 0, r.width, title));
			}
		}

		// ---- keyboard
		private void HandleKeys(List<ModInfo> shown) {
			Event e = Event.current;
			if (e.type != EventType.KeyDown) {
				return;
			}
			bool inField = GUIUtility.keyboardControl != 0;
			bool inFilter = GUI.GetNameOfFocusedControl() == FilterControl;
			if (e.keyCode == KeyCode.Escape) {
				if (inField) {
					GUIUtility.keyboardControl = 0;
				} else {
					Toggle();
				}
				e.Use();
				return;
			}
			if (e.keyCode == KeyCode.F && (e.control || e.command) && view_ == View.Mods) {
				config_.ListCollapsed = false;
				GUI.FocusControl(FilterControl);
				e.Use();
				return;
			}
			// typing in a field (the filter, or a mod's page): the keys are the field's, but Down / Enter leave the filter
			if (inField && !(inFilter && (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))) {
				return;
			}
			if (view_ != View.Mods) {
				return;
			}
			int index = shown.FindIndex(m => m.Folder == selected_);
			switch (e.keyCode) {
				case KeyCode.DownArrow:
				case KeyCode.UpArrow:
					if (shown.Count > 0) {
						index = Mathf.Clamp(index + (e.keyCode == KeyCode.DownArrow ? 1 : -1), 0, shown.Count - 1);
						Select(shown[index], index, true);
					}
					GUIUtility.keyboardControl = 0;
					e.Use();
					break;
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					GUIUtility.keyboardControl = 0;
					keyboardFocus_ = true;
					e.Use();
					break;
				case KeyCode.Space:
					if (index >= 0 && !IsBroken(shown[index])) {
						SetEnabled(shown[index], config_.Disabled.Contains(shown[index].Id));
					}
					e.Use();
					break;
			}
		}

		private void Select(ModInfo m, int index, bool byKeyboard) {
			if (selected_ != m.Folder) {
				detailScroll_ = Vector2.zero;
			}
			selected_ = m.Folder;
			expanded_.Add(m.Folder);
			keyboardFocus_ = byKeyboard;
			view_ = View.Mods;
			if (byKeyboard) {
				float row = skin_.T(22);
				treeScroll_.y = Mathf.Clamp(treeScroll_.y, (index + 1) * row - treeViewHeight_, index * row);
			}
		}

		// ---- title bar
		private void DrawTitleBar() {
			GUILayout.BeginHorizontal(skin_.TitleBar, GUILayout.Height(skin_.T(TitleHeight)));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			Rect bar = GUILayoutUtility.GetLastRect();
			float y = (bar.height - skin_.Title.CalcHeight(new GUIContent("A"), 100)) / 2;
			var title = new GUIContent("Aether Loader");
			float w = skin_.Title.CalcSize(title).x;
			GUI.Label(new Rect(skin_.S(12), y, w, bar.height), title, skin_.Title);
			GUI.Label(new Rect(skin_.S(12) + w + skin_.S(8), y - skin_.S(1), skin_.S(200), bar.height),
				"v" + LoaderPlugin.Version + "  ·  Aether.API " + AetherApi.Version, skin_.Version);
			// close, maximise / restore
			GUIStyle icon = IconStyle();
			float b = skin_.S(36);
			var close = new Rect(bar.width - b, 0, b, bar.height);
			if (GUI.Button(close, new GUIContent(skin_.Cross, "Close (" + config_.Hotkey + ")"), icon)) {
				Toggle();
			}
			var max = new Rect(close.x - b, 0, b, bar.height);
			if (GUI.Button(max, new GUIContent(config_.Maximized ? skin_.Restore : skin_.Maximize, config_.Maximized ? "Restore" : "Maximise"), icon)) {
				ToggleMaximized();
			}
		}

		private void ToggleMaximized() {
			config_.Maximized = !config_.Maximized;
			Save();
		}

		private GUIStyle iconStyle_;
		private UiSkin iconSkin_;

		/// <summary>A flat button that shows only its icon (title bar, tree).</summary>
		private GUIStyle IconStyle() {
			if (iconStyle_ == null || !ReferenceEquals(iconSkin_, skin_)) {
				iconSkin_ = skin_;
				iconStyle_ = new GUIStyle(skin_.Button) { normal = { background = null }, imagePosition = ImagePosition.ImageOnly, margin = new RectOffset() };
				iconStyle_.hover.background = skin_.Button.hover.background;
				iconStyle_.padding = new RectOffset(skin_.S(10), skin_.S(10), skin_.S(8), skin_.S(8));
			}
			return iconStyle_;
		}

		// ---- activity bar
		private void DrawActivityBar() {
			GUILayout.BeginVertical();
			Activity(View.Mods, skin_.ModsIcon, "Mods");
			Activity(View.Settings, skin_.SettingsIcon, "Settings");
			GUILayout.EndVertical();
		}

		private void Activity(View v, Texture2D icon, string tip) {
			bool on = view_ == v;
			Color was = GUI.color;
			if (!on) {
				GUI.color = new Color(was.r, was.g, was.b, was.a * 0.55f);
			}
			if (GUILayout.Button(new GUIContent(icon, tip), on ? skin_.ActivityActive : skin_.Activity)) {
				if (on && v == View.Mods) {
					config_.ListCollapsed = !config_.ListCollapsed; // VS Code: the shown view's icon folds its side bar
					Save();
				} else {
					view_ = v;
					GUIUtility.keyboardControl = 0;
				}
			}
			GUI.color = was;
		}

		// ---- side bar: the mod tree
		private void DrawTree(List<ModInfo> mods, List<ModInfo> shown) {
			// the header: what this is, and folding it away
			GUILayout.BeginHorizontal(GUILayout.Height(skin_.T(40)));
			GUILayout.Label("Mods  " + Paint(UiSkin.Secondary, mods.Count.ToString()), skin_.Header, GUILayout.Height(skin_.T(40)));
			GUILayout.FlexibleSpace();
			if (IconButton(skin_.Collapse, "Hide the mod list")) {
				SetCollapsed(true);
			}
			GUILayout.Space(skin_.S(6));
			GUILayout.EndHorizontal();
			GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(skin_.S(10), skin_.S(10), 0, skin_.S(8)) });
			GUI.SetNextControlName(FilterControl);
			search_ = GUILayout.TextField(search_, 64, skin_.Input);
			if (search_.Length == 0 && GUI.GetNameOfFocusedControl() != FilterControl) {
				GUI.Label(GUILayoutUtility.GetLastRect(), "Filter (Ctrl+F)", skin_.Placeholder);
			}
			GUILayout.EndVertical();

			treeScroll_ = GUILayout.BeginScrollView(treeScroll_, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			if (mods.Count == 0) {
				GUILayout.Label("No mods installed. Each mod is a folder with a mod.json in the mods folder.", skin_.Small);
			} else if (shown.Count == 0) {
				GUILayout.Label("No mods match.", skin_.Small);
			}
			for (int i = 0; i < shown.Count; ++i) {
				ModInfo m = shown[i];
				bool isSel = m.Folder == selected_;
				bool open = expanded_.Contains(m.Folder);
				IReadOnlyList<IModPanel> panels = Panels(m);
				int page = Page(m, panels.Count);
				bool rowSel = isSel && !open; // an open mod shows its chosen page marked instead
				GUILayout.BeginHorizontal(rowSel ? (keyboardFocus_ ? skin_.RowFocus : skin_.TreeRowSelected) : skin_.TreeRow);
				Rect chevron = GUILayoutUtility.GetRect(skin_.S(22), skin_.T(28), GUILayout.Width(skin_.S(22)));
				GUI.DrawTexture(new Rect(chevron.x + skin_.S(4), chevron.y + (chevron.height - skin_.S(16)) / 2, skin_.S(16), skin_.S(16)), open ? skin_.ChevronDown : skin_.ChevronRight);
				GUILayout.Label(Escape(m.Name) + (WillChange(m) ? Paint(UiSkin.Warning, "  ●") : ""), skin_.Cell, GUILayout.ExpandWidth(true));
				GUILayout.Label(Paint(StateColor(m), "●"), skin_.Disc);
				GUILayout.EndHorizontal();
				Rect row = GUILayoutUtility.GetLastRect();
				Event e = Event.current;
				if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition)) {
					if (chevron.Contains(e.mousePosition) || isSel && open) {
						if (!expanded_.Remove(m.Folder)) {
							expanded_.Add(m.Folder);
						}
					} else {
						Select(m, i, false);
						page_[m.Folder] = 0;
					}
					selected_ = m.Folder;
					GUIUtility.keyboardControl = 0;
					e.Use();
				}
				if (!open) {
					continue;
				}
				for (int p = 0; p <= panels.Count; ++p) {
					string title = p == 0 ? "Details" : panels[p - 1].Title ?? "Page";
					bool chosen = isSel && page == p;
					if (GUILayout.Button(title, chosen ? skin_.TreeChildOn : skin_.TreeChild, GUILayout.ExpandWidth(true))) {
						selected_ = m.Folder;
						page_[m.Folder] = p;
						keyboardFocus_ = false;
						GUIUtility.keyboardControl = 0;
					}
				}
				GUILayout.Space(skin_.S(4));
			}
			GUILayout.EndScrollView();
			if (Event.current.type == EventType.Repaint) {
				treeViewHeight_ = GUILayoutUtility.GetLastRect().height;
			}
		}

		/// <summary>A small icon button (folding the list).</summary>
		private GUIStyle smallIcon_;
		private UiSkin smallIconSkin_;

		private bool IconButton(Texture2D icon, string tip) {
			if (smallIcon_ == null || !ReferenceEquals(smallIconSkin_, skin_)) {
				smallIconSkin_ = skin_;
				smallIcon_ = new GUIStyle(skin_.StatusItem) { imagePosition = ImagePosition.ImageOnly, padding = new RectOffset(skin_.S(6), skin_.S(6), skin_.S(6), skin_.S(6)), fixedHeight = 0 };
			}
			GUIStyle st = smallIcon_;
			Rect r = GUILayoutUtility.GetRect(skin_.S(28), skin_.S(28), GUILayout.Width(skin_.S(28)), GUILayout.Height(skin_.T(40)));
			return GUI.Button(new Rect(r.x, r.y + (r.height - skin_.S(28)) / 2, skin_.S(28), skin_.S(28)), new GUIContent(icon, tip), st);
		}

		private void SetCollapsed(bool collapsed) {
			config_.ListCollapsed = collapsed;
			Save();
		}
		private IReadOnlyList<IModPanel> Panels(ModInfo m) => m.State == ModState.Loaded ? loader_.PanelsOf(m) : Array.Empty<IModPanel>();

		private int Page(ModInfo m, int panels) {
			page_.TryGetValue(m.Folder, out int p);
			return p > panels ? 0 : p;
		}

		/// <summary>The line between the side bar and the editor: drag it to make the side bar wider or narrower.</summary>
		private void DrawSash(Rect sash) {
			int control = GUIUtility.GetControlID(FocusType.Passive);
			Event e = Event.current;
			switch (e.GetTypeForControl(control)) {
				case EventType.MouseDown:
					if (e.button == 0 && sash.Contains(e.mousePosition)) {
						GUIUtility.hotControl = control;
						sashDrag_ = true;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (sashDrag_ && GUIUtility.hotControl == control) {
						config_.ListWidth = Mathf.Max(MinList, config_.ListWidth + e.delta.x / skin_.Scale);
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (sashDrag_ && GUIUtility.hotControl == control) {
						GUIUtility.hotControl = 0;
						sashDrag_ = false;
						config_.ListWidth = Mathf.Min(config_.ListWidth, windowPixels_.width * 0.5f / skin_.Scale);
						e.Use();
						Save();
					}
					break;
				case EventType.Repaint:
					if (sashDrag_ || sash.Contains(e.mousePosition)) {
						GUI.DrawTexture(new Rect(sash.x + sash.width / 2 - skin_.S(1), sash.y, skin_.S(2), sash.height), skin_.Sash);
					}
					break;
			}
		}

		// ---- editor: the selected mod's page
		private void DrawEditor(ModInfo m) {
			if (m == null) {
				GUILayout.Label("Select a mod.", skin_.Text);
				return;
			}
			IReadOnlyList<IModPanel> panels = Panels(m);
			int page = Page(m, panels.Count);
			string pageTitle = page == 0 ? "Details" : panels[page - 1].Title ?? "Page";
			// where one is (and, with the list folded away, the way back to it)
			GUILayout.BeginHorizontal(GUILayout.Height(skin_.T(40)));
			if (config_.ListCollapsed) {
				GUILayout.Space(skin_.S(8));
				if (IconButton(skin_.Expand, "Show the mod list")) {
					SetCollapsed(false);
				}
			}
			GUILayout.Label(Escape(m.Name) + Paint(UiSkin.Secondary, "   /   ") + Escape(pageTitle), skin_.Breadcrumb, GUILayout.ExpandWidth(false));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Box(GUIContent.none, skin_.Divider, GUILayout.ExpandWidth(true));
			if (page > 0) {
				DrawPanel(m, panels[page - 1]);
			} else {
				DrawDetails(m);
			}
		}
		// ---- a mod's page: its own GUILayout code inside a scroll view, exceptions shown in place
		private void DrawPanel(ModInfo m, IModPanel panel) {
			if (ui_ == null || !ReferenceEquals(uiSkin_, skin_)) {
				ui_ = skin_.ToModUi(Toggle);
				uiSkin_ = skin_;
			}
			string key = m.Id + "/" + panel.Title;
			panelScroll_.TryGetValue(key, out Vector2 scroll);
			scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			panelScroll_[key] = scroll;
			GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(skin_.S(16), skin_.S(16), skin_.S(10), skin_.S(10)) });
			if (panelErrors_.TryGetValue(key, out string error)) {
				GUILayout.Label("The page stopped: " + Escape(error), skin_.ErrorBox);
				if (GUILayout.Button("Retry", skin_.Button, GUILayout.ExpandWidth(false))) {
					panelErrors_.Remove(key);
				}
			} else {
				try {
					panel.Draw(ui_);
				} catch (ExitGUIException) {
					throw;
				} catch (Exception ex) {
					panelErrors_[key] = ex.GetType().Name + ": " + ex.Message;
					BepInEx.Logging.Logger.CreateLogSource("Aether Loader").LogError(m.Id + ": page \"" + panel.Title + "\" threw: " + ex);
				}
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
		}

		private readonly Dictionary<string, Vector2> panelScroll_ = new Dictionary<string, Vector2>();
		private readonly Dictionary<string, string> panelErrors_ = new Dictionary<string, string>();
		private ModUi ui_;
		private UiSkin uiSkin_;

		private void DrawDetails(ModInfo m) {
			detailScroll_ = GUILayout.BeginScrollView(detailScroll_, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			// the name, and beside it the switch that enables or disables the mod (from the next start)
			GUILayout.BeginHorizontal();
			GUILayout.Label(Escape(m.Name), skin_.Name, GUILayout.ExpandWidth(false));
			if (!IsBroken(m)) {
				bool on = !config_.Disabled.Contains(m.Id);
				GUILayout.BeginVertical();
				GUILayout.Space(skin_.S(20)); // the name's top padding
				Rect line = GUILayoutUtility.GetRect(skin_.S(30), skin_.Name.CalcHeight(new GUIContent("A"), 100) - skin_.Name.padding.vertical, GUILayout.Width(skin_.S(30)));
				GUILayout.EndVertical();
				var sw = new Rect(line.x, line.y + (line.height - skin_.S(16)) / 2, skin_.S(30), skin_.S(16));
				if (GUI.Button(sw, new GUIContent(on ? skin_.SwitchOn : skin_.SwitchOff, on ? "On: disable it from the next start" : "Off: enable it from the next start"), GUIStyle.none)) {
					SetEnabled(m, !on);
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Label(Escape(m.Id) + (IsBroken(m) ? "" : "   " + m.Version), skin_.Id);
			// its folders
			GUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(skin_.S(24), skin_.S(24), 0, skin_.S(4)) });
			if (GUILayout.Button("Open folder", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(m.Directory);
			}
			if (!IsBroken(m) && GUILayout.Button("Open config", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(loader_.ConfigDirectory(m));
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			if (WillChange(m)) {
				GUILayout.Label(config_.Disabled.Contains(m.Id) ? "Disabled: it won't load after the game restarts." : "Enabled: it loads after the game restarts.", skin_.Notice);
			}
			if (IsProblem(m) && !string.IsNullOrEmpty(m.Reason)) {
				GUILayout.Label(Escape(m.Reason), skin_.ErrorBox);
			}

			GUILayout.Label("Information", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			Field("State", Paint(StateColor(m), StateCode(m)), skin_.ValueMono);
			if (!IsBroken(m)) {
				Field("Version", m.Version.ToString(), skin_.ValueMono);
			}
			if (!string.IsNullOrEmpty(m.Author)) {
				Field("Author", Escape(m.Author), skin_.Value);
			}
			Field("Folder", "mods\\" + Escape(m.Folder), skin_.ValueMono);
			if (!IsBroken(m)) {
				Field("Config", "config\\" + Escape(m.Id), skin_.ValueMono);
			}
			GUILayout.EndVertical();
			if (!string.IsNullOrEmpty(m.Description)) {
				GUILayout.Label("Description", skin_.Section);
				GUILayout.Label(Escape(m.Description), skin_.Text);
			}

			GUILayout.Label("Requires", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			if (m.Dependencies.Count == 0) {
				GUILayout.Label(Paint(UiSkin.Secondary, "Nothing"), skin_.FieldLabel);
			}
			foreach (ModDependency d in m.Dependencies) {
				ModInfo dep = loader_.Find(d.Id);
				string state;
				if (dep == null) {
					state = Paint(d.Optional ? UiSkin.Secondary : UiSkin.Error, "missing");
				} else if (!d.Range.Contains(dep.Version)) {
					state = Paint(UiSkin.Error, dep.Version + " out of range");
				} else {
					state = dep.Version + " " + Paint(StateColor(dep), StateCode(dep));
				}
				string range = d.Range.ToString() == "*" ? "any" : d.Range.ToString();
				Field(Escape(d.Id), Paint(UiSkin.Secondary, Escape(range) + (d.Optional ? "  optional" : "")) + "   " + state, skin_.ValueMono, true);
			}
			GUILayout.EndVertical();

			List<ModInfo> dependents = loader_.Dependents(m.Id);
			GUILayout.Label("Required by", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			if (dependents.Count == 0) {
				GUILayout.Label(Paint(UiSkin.Secondary, "Nothing"), skin_.FieldLabel);
			}
			foreach (ModInfo d in dependents) {
				Field(Escape(d.Id), Paint(StateColor(d), StateCode(d)), skin_.ValueMono, true);
			}
			GUILayout.EndVertical();
			var stranded = dependents.Where(d => !config_.Disabled.Contains(d.Id)).ToList();
			if (config_.Disabled.Contains(m.Id) && stranded.Count > 0) {
				GUILayout.Label("While this mod is disabled, these don't load either: " + string.Join(", ", stranded.Select(d => Escape(d.Name))), skin_.Notice);
			}
			GUILayout.Space(skin_.S(16));
			GUILayout.EndScrollView();
		}

		/// <summary>A row of a card: a name, then its value (wide: the name takes the room an id needs).</summary>
		private void Field(string key, string value, GUIStyle style, bool wide = false) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(key, skin_.Key, GUILayout.Width(skin_.T(wide ? 260 : KeyWidth)));
			GUILayout.Label(value, style);
			GUILayout.EndHorizontal();
		}

		// ---- settings
		private void DrawSettings(List<ModInfo> mods) {
			GUILayout.BeginHorizontal(GUILayout.Height(skin_.T(40)));
			GUILayout.Label("Settings", skin_.Breadcrumb);
			GUILayout.EndHorizontal();
			GUILayout.Box(GUIContent.none, skin_.Divider, GUILayout.ExpandWidth(true));
			settingsScroll_ = GUILayout.BeginScrollView(settingsScroll_, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

			GUILayout.Label("Window", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			// size: steps (a slider would make the fonts again on every move)
			SettingRow("Size", () => {
				if (GUILayout.Button("−", skin_.Button, GUILayout.Width(skin_.S(34)))) {
					SetScale(Mathf.Max(0.75f, Scale - 0.1f));
				}
				GUILayout.Label(Scale.ToString("0.0#") + "×", skin_.ValueMono, GUILayout.Width(skin_.T(52)));
				if (GUILayout.Button("+", skin_.Button, GUILayout.Width(skin_.S(34)))) {
					SetScale(Mathf.Min(3f, Scale + 0.1f));
				}
				GUILayout.Space(skin_.S(8));
				if (GUILayout.Button("Auto", config_.UiScale <= 0 ? skin_.ButtonPrimary : skin_.Button, GUILayout.ExpandWidth(false))) {
					SetScale(0);
				}
			});
			RowLine();
			SettingRow("Text size", () => {
				foreach (var (label, size) in new[] { ("Normal", 1f), ("Large", 1.2f), ("Larger", 1.4f) }) {
					bool on = Mathf.Approximately(config_.TextSize, size);
					if (GUILayout.Button(label, on ? skin_.ButtonPrimary : skin_.Button, GUILayout.ExpandWidth(false)) && !on) {
						config_.TextSize = size;
						Save();
					}
				}
			});
			RowLine();
			SettingRow("Opacity", () => {
				float o = Slider(config_.Opacity, 0.3f, 1f);
				GUILayout.Label(Mathf.RoundToInt(o * 100) + "%", skin_.ValueMono, GUILayout.Width(skin_.T(52)));
				if (!Mathf.Approximately(o, config_.Opacity)) {
					config_.Opacity = Mathf.Round(o * 100) / 100;
					alpha_ = config_.Opacity;
					settingsDirty_ = true;
				}
			});
			RowLine();
			SettingRow("Fade while the pointer is away", () => {
				if (Switch(config_.FadeAway)) {
					config_.FadeAway = !config_.FadeAway;
					Save();
				}
				GUILayout.Space(skin_.S(8));
				float f = Slider(config_.FadeOpacity, 0.1f, 1f);
				GUILayout.Label(Mathf.RoundToInt(f * 100) + "%", skin_.ValueMono, GUILayout.Width(skin_.T(52)));
				if (!Mathf.Approximately(f, config_.FadeOpacity)) {
					config_.FadeOpacity = Mathf.Round(f * 100) / 100;
					settingsDirty_ = true;
				}
			});
			RowLine();
			SettingRow("Size and place", () => {
				if (GUILayout.Button(config_.Maximized ? "Restore" : "Maximise", skin_.Button, GUILayout.ExpandWidth(false))) {
					ToggleMaximized();
				}
				if (GUILayout.Button("Reset", skin_.Button, GUILayout.ExpandWidth(false))) {
					config_.Window = LoaderConfig.DefaultWindow;
					config_.Maximized = false;
					Save();
				}
			});
			GUILayout.EndVertical();

			GUILayout.Label("Input", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			SettingRow("Keep the mouse from the game over the window", () => {
				if (Switch(config_.BlockMouse)) {
					config_.BlockMouse = !config_.BlockMouse;
					Save();
				}
			});
			RowLine();
			SettingRow("Keep keys from the game while you type here", () => {
				if (Switch(config_.BlockKeys)) {
					config_.BlockKeys = !config_.BlockKeys;
					Save();
				}
			});
			RowLine();
			SettingRow("Open and close", () => {
				if (GUILayout.Button(rebinding_ ? "Press a key   (Esc keeps " + config_.Hotkey + ")" : config_.Hotkey.ToString(), rebinding_ ? skin_.ButtonPrimary : skin_.Button, GUILayout.ExpandWidth(false))) {
					rebinding_ = !rebinding_;
				}
			});
			GUILayout.EndVertical();
			GUILayout.Label("With these on, the game's buttons, camera and shortcuts don't react to what you do in this window.", skin_.Small);

			GUILayout.Label("About", skin_.Section);
			GUILayout.BeginVertical(skin_.Card);
			Field("Loader", LoaderPlugin.Version, skin_.ValueMono);
			Field("API", AetherApi.Version.ToString(), skin_.ValueMono);
			Field("Mods", mods.Count + " installed", skin_.Value);
			GUILayout.EndVertical();
			GUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(skin_.S(24), skin_.S(24), skin_.S(8), skin_.S(16)) });
			if (GUILayout.Button("Open mods folder", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(loader_.ModsDirectory);
			}
			if (GUILayout.Button("Open settings folder", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(config_.Directory);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndScrollView();
			// the sliders are saved when they are let go
			if (settingsDirty_ && Event.current.type == EventType.MouseUp) {
				settingsDirty_ = false;
				Save();
			}
		}

		private bool settingsDirty_;

		private void SetScale(float s) {
			config_.UiScale = s <= 0 ? 0 : Mathf.Round(s * 20) / 20;
			Save();
		}

		private void SettingRow(string label, Action controls) {
			GUILayout.BeginHorizontal(GUILayout.MinHeight(skin_.T(44)));
			GUILayout.Label(label, skin_.FieldLabel, GUILayout.Width(skin_.T(320)), GUILayout.Height(skin_.T(44)));
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			controls();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		// a hairline between the rows of a card
		private void RowLine() {
			Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
			if (Event.current.type == EventType.Repaint) {
				GUI.DrawTexture(new Rect(r.x + skin_.S(14), r.y, r.width - skin_.S(28), 1), skin_.Divider.normal.background);
			}
		}

		private float Slider(float value, float min, float max) =>
			GUILayout.HorizontalSlider(value, min, max, skin_.SliderTrack, skin_.SliderThumb, GUILayout.Width(skin_.S(200)));

		/// <summary>A switch; true when it was clicked.</summary>
		private bool Switch(bool on) {
			Rect r = GUILayoutUtility.GetRect(skin_.S(38), skin_.T(34), GUILayout.Width(skin_.S(38)));
			return GUI.Button(new Rect(r.x, r.y + (r.height - skin_.S(16)) / 2, skin_.S(30), skin_.S(16)), on ? skin_.SwitchOn : skin_.SwitchOff, GUIStyle.none);
		}
		// ---- status bar
		private void DrawStatusBar(List<ModInfo> mods) {
			GUILayout.BeginHorizontal();
			int ok = mods.Count(m => m.State == ModState.Loaded), off = mods.Count(m => m.State == ModState.Disabled);
			int skip = mods.Count(m => m.State == ModState.Skipped), fail = mods.Count(m => m.State == ModState.Failed);
			GUILayout.Label(ok + " ok   " + off + " off   " + skip + " skip   " + fail + " fail", skin_.StatusText, GUILayout.ExpandWidth(false));
			int pending = mods.Count(WillChange);
			if (message_ != null) {
				GUILayout.Label("│  " + Escape(message_), skin_.StatusText, GUILayout.ExpandWidth(false));
			} else if (pending > 0) {
				GUILayout.Label("│  " + pending + (pending == 1 ? " change" : " changes") + " pending, applies after restart", skin_.StatusText, GUILayout.ExpandWidth(false));
				if (GUILayout.Button("Undo", skin_.StatusItem, GUILayout.ExpandWidth(false))) {
					config_.Disabled.Clear();
					config_.Disabled.UnionWith(config_.DisabledAtStart);
					Save();
					loader_.WriteStatus();
				}
			}
			GUILayout.FlexibleSpace();
			if (InputGate.BlockMouse || InputGate.BlockKeys) {
				GUILayout.Label("Game input held for the window", skin_.StatusText, GUILayout.ExpandWidth(false));
			}
			if (GUILayout.Button("Mods Folder", skin_.StatusItem, GUILayout.ExpandWidth(false))) {
				OpenFolder(loader_.ModsDirectory);
			}
			if (GUILayout.Button(config_.Hotkey + " Close", skin_.StatusItem, GUILayout.ExpandWidth(false))) {
				Toggle();
			}
			GUILayout.Space(skin_.S(12)); // the resize corner
			GUILayout.EndHorizontal();
		}

		private void DrawResizeHandle(Rect window) {
			float size = skin_.S(12);
			var handle = new Rect(window.width - size, window.height - size, size, size);
			int control = GUIUtility.GetControlID(FocusType.Passive);
			Event e = Event.current;
			switch (e.GetTypeForControl(control)) {
				case EventType.MouseDown:
					if (handle.Contains(e.mousePosition)) {
						GUIUtility.hotControl = control;
						resizing_ = true;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (resizing_ && GUIUtility.hotControl == control) {
						resizeDelta_ += e.delta;
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (resizing_ && GUIUtility.hotControl == control) {
						GUIUtility.hotControl = 0;
						resizing_ = false;
						e.Use();
						Save();
					}
					break;
			}
		}

		// ---- actions
		private void SetEnabled(ModInfo m, bool on) {
			if (on) {
				config_.Disabled.Remove(m.Id);
			} else {
				config_.Disabled.Add(m.Id);
			}
			Save();
			loader_.WriteStatus();
		}

		private void Save() {
			try {
				config_.Save();
				message_ = null;
			} catch (Exception ex) {
				message_ = "loader.json: " + ex.Message;
			}
		}

		private void OpenFolder(string path) {
			try {
				Directory.CreateDirectory(path);
				Application.OpenURL(new Uri(path).AbsoluteUri);
			} catch (Exception ex) {
				message_ = path + ": " + ex.Message;
			}
		}

		// ---- helpers
		private bool Matches(ModInfo m) {
			if (search_.Length == 0) {
				return true;
			}
			return m.Name.IndexOf(search_, StringComparison.OrdinalIgnoreCase) >= 0 || m.Id.IndexOf(search_, StringComparison.OrdinalIgnoreCase) >= 0 ||
				(m.Author ?? "").IndexOf(search_, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsProblem(ModInfo m) => m.State == ModState.Skipped || m.State == ModState.Failed;
		private static bool IsBroken(ModInfo m) => m.State == ModState.Skipped && m.Version.Equals(default(ModVersion));

		private bool WillChange(ModInfo m) => config_.DisabledAtStart.Contains(m.Id) != config_.Disabled.Contains(m.Id);

		private static string StateCode(ModInfo m) {
			switch (m.State) {
				case ModState.Loaded: return "ok";
				case ModState.Disabled: return "off";
				case ModState.Failed: return "fail";
				default: return "skip";
			}
		}

		private static Color StateColor(ModInfo m) {
			switch (m.State) {
				case ModState.Loaded: return UiSkin.Ok;
				case ModState.Disabled: return UiSkin.Off;
				case ModState.Failed: return UiSkin.Error;
				default: return UiSkin.Warning;
			}
		}

		private static string Paint(Color c, string text) => "<color=#" + UiSkin.Html(c) + ">" + text + "</color>";

		// (names and reasons come from mod.json: no rich text tags from there)
		private static string Escape(string s) => (s ?? "").Replace("<", "<​");
	}
}
