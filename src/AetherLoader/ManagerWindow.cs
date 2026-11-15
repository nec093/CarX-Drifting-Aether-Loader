using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aether;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// The mod manager (F9 by default, loader.json "hotkey"), laid out like a VS Code side bar and editor: the mod list on
	/// the left (checkbox, state, name, version; filter field and state tabs above), the selected mod on the right (fields,
	/// why it did not load, what it requires and what requires it, buttons), a status bar at the bottom (counts, pending
	/// changes with undo, mods folder, close).
	/// Keyboard: Up / Down select, Space switches the selected mod, Ctrl+F filters, Esc leaves the filter or closes.
	/// Switches take effect at the next start. The window moves (title bar) and resizes (bottom right corner); its place is
	/// kept in loader.json in 1440p units. The UI scale follows the screen height (1 at 1440p) or loader.json "uiScale";
	/// fonts are rasterised at the final size.
	/// </summary>
	internal sealed class ManagerWindow : MonoBehaviour {
		private enum Filter { All, Ok, Off, Problems }

		private const int WindowId = 0x41454C44;
		private const string FilterControl = "aether-loader-filter";
		private const float MinWidth = 640, MinHeight = 360, TitleHeight = 30, KeyWidth = 72, ColState = 44, ColVersion = 64;

		private ModLoader loader_;
		private LoaderConfig config_;
		private UiSkin skin_;
		private bool open_;
		private Filter filter_;
		private string search_ = "";
		private string selected_; // folder of the selected mod
		private bool keyboardFocus_;
		private Vector2 listScroll_, detailScroll_;
		private float listViewHeight_ = 300;
		private bool resizing_;
		private Vector2 resizeDelta_;
		private bool cursorVisible_;
		private CursorLockMode cursorLock_;
		private string message_;

		public void Init(ModLoader loader, LoaderConfig config) {
			loader_ = loader;
			config_ = config;
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
			if (open_) {
				cursorVisible_ = Cursor.visible;
				cursorLock_ = Cursor.lockState;
			} else {
				Cursor.visible = cursorVisible_;
				Cursor.lockState = cursorLock_;
				GUIUtility.keyboardControl = 0;
				Save(); // the window's place
			}
		}

		private float Scale => config_.UiScale > 0 ? config_.UiScale : Mathf.Clamp(Screen.height / 1440f, 1f, 3f);

		private void OnGUI() {
			Event e = Event.current;
			// key events reach IMGUI with either of Unity's input handlers
			if (e.type == EventType.KeyDown && e.keyCode == config_.Hotkey) {
				Toggle();
				e.Use();
			}
			if (!open_) {
				return;
			}
			float scale = Scale;
			if (skin_ == null || !Mathf.Approximately(skin_.Scale, scale)) {
				skin_ = UiSkin.Create(scale);
			}
			// the window in screen pixels (config keeps 1440p units)
			Rect b = config_.Window;
			b.width = Mathf.Max(b.width, MinWidth);
			b.height = Mathf.Max(b.height, MinHeight);
			var r = new Rect(b.x * scale, b.y * scale, b.width * scale, b.height * scale);
			r.width = Mathf.Min(r.width, Screen.width);
			r.height = Mathf.Min(r.height, Screen.height);
			r.x = Mathf.Clamp(r.x, 0, Screen.width - r.width);
			r.y = Mathf.Clamp(r.y, 0, Screen.height - r.height);
			windowPixels_ = r;
			resizeDelta_ = Vector2.zero;
			r = GUI.Window(WindowId, r, DrawWindow, GUIContent.none, skin_.Window);
			r.size += resizeDelta_;
			config_.Window = new Rect(r.x / scale, r.y / scale, r.width / scale, r.height / scale);
		}

		private Rect windowPixels_;

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
			if (selected_ == null || !shown.Any(m => m.Folder == selected_)) {
				selected_ = shown.FirstOrDefault()?.Folder;
			}
			HandleKeys(shown);
			float title = skin_.S(TitleHeight);
			float status = skin_.S(22);
			float listWidth = Mathf.Round(r.width * 0.46f);

			GUILayout.BeginArea(new Rect(1, 1, r.width - 2, title));
			DrawTitleBar();
			GUILayout.EndArea();
			GUILayout.BeginArea(new Rect(1, 1 + title, listWidth, r.height - 2 - title - status), skin_.Sidebar);
			DrawSidebar(mods, shown);
			GUILayout.EndArea();
			GUILayout.BeginArea(new Rect(1 + listWidth, 1 + title, r.width - 2 - listWidth, r.height - 2 - title - status), skin_.Editor);
			DrawDetails(mods.FirstOrDefault(m => m.Folder == selected_));
			GUILayout.EndArea();
			GUILayout.BeginArea(new Rect(1, r.height - 1 - status, r.width - 2, status), skin_.StatusBar);
			DrawStatusBar(mods);
			GUILayout.EndArea();
			DrawResizeHandle(r);
			GUI.DragWindow(new Rect(0, 0, r.width, title));
		}

		// ---- keyboard
		private void HandleKeys(List<ModInfo> shown) {
			Event e = Event.current;
			if (e.type != EventType.KeyDown) {
				return;
			}
			bool inFilter = GUI.GetNameOfFocusedControl() == FilterControl;
			if (e.keyCode == KeyCode.Escape) {
				if (inFilter) {
					GUIUtility.keyboardControl = 0;
				} else {
					Toggle();
				}
				e.Use();
				return;
			}
			if (e.keyCode == KeyCode.F && (e.control || e.command)) {
				GUI.FocusControl(FilterControl);
				e.Use();
				return;
			}
			if (inFilter && e.keyCode != KeyCode.DownArrow && e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) {
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
			selected_ = m.Folder;
			keyboardFocus_ = byKeyboard;
			detailScroll_ = Vector2.zero;
			if (byKeyboard) {
				float row = skin_.S(22);
				listScroll_.y = Mathf.Clamp(listScroll_.y, (index + 1) * row - listViewHeight_, index * row);
			}
		}

		// ---- title bar
		private void DrawTitleBar() {
			GUILayout.BeginHorizontal(skin_.TitleBar, GUILayout.Height(skin_.S(TitleHeight)));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			Rect bar = GUILayoutUtility.GetLastRect();
			float y = (bar.height - skin_.Title.CalcHeight(new GUIContent("A"), 100)) / 2;
			var title = new GUIContent("AETHER LOADER");
			float w = skin_.Title.CalcSize(title).x;
			GUI.Label(new Rect(skin_.S(12), y, w, bar.height), title, skin_.Title);
			GUI.Label(new Rect(skin_.S(12) + w + skin_.S(8), y - skin_.S(1), skin_.S(200), bar.height),
				"v" + LoaderPlugin.Version + "  ·  Aether.API " + AetherApi.Version, skin_.Version);
			// close
			var close = new Rect(bar.width - skin_.S(36), 0, skin_.S(36), bar.height);
			var closeStyle = new GUIStyle(skin_.Button) { normal = { background = null }, fontSize = skin_.S(14), padding = new RectOffset(), margin = new RectOffset() };
			closeStyle.hover.background = skin_.Button.hover.background;
			closeStyle.imagePosition = ImagePosition.ImageOnly;
			closeStyle.padding = new RectOffset(skin_.S(10), skin_.S(10), skin_.S(7), skin_.S(7));
			if (GUI.Button(close, skin_.Cross, closeStyle)) {
				Toggle();
			}
		}

		// ---- side bar: filter, tabs, list
		private void DrawSidebar(List<ModInfo> mods, List<ModInfo> shown) {
			GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(skin_.S(8), skin_.S(8), skin_.S(8), 0) });
			GUI.SetNextControlName(FilterControl);
			search_ = GUILayout.TextField(search_, 64, skin_.Input);
			if (search_.Length == 0 && GUI.GetNameOfFocusedControl() != FilterControl) {
				GUI.Label(GUILayoutUtility.GetLastRect(), "Filter mods (Ctrl+F)", skin_.Placeholder);
			}
			GUILayout.Space(skin_.S(4));
			GUILayout.BeginHorizontal();
			Tab(Filter.All, "ALL", mods.Count);
			Tab(Filter.Ok, "OK", mods.Count(m => m.State == ModState.Loaded));
			Tab(Filter.Off, "OFF", mods.Count(m => m.State == ModState.Disabled));
			Tab(Filter.Problems, "PROBLEMS", mods.Count(IsProblem));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.Box(GUIContent.none, skin_.Divider, GUILayout.ExpandWidth(true));

			listScroll_ = GUILayout.BeginScrollView(listScroll_, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			if (mods.Count == 0) {
				GUILayout.Label("No mods installed.", skin_.Text);
				GUILayout.Label("Each mod is a folder with a mod.json in the mods folder.", skin_.Text);
			} else if (shown.Count == 0) {
				GUILayout.Label("No mods match.", skin_.Text);
			}
			for (int i = 0; i < shown.Count; ++i) {
				ModInfo m = shown[i];
				bool selected = m.Folder == selected_;
				GUILayout.BeginHorizontal(selected ? (keyboardFocus_ ? skin_.RowFocus : skin_.RowSelected) : skin_.Row);
				if (IsBroken(m)) {
					GUILayout.Space(skin_.S(30));
				} else {
					bool on = !config_.Disabled.Contains(m.Id);
					if (on ? GUILayout.Button(skin_.Tick, skin_.CheckOn) : GUILayout.Button(GUIContent.none, skin_.Check)) {
						SetEnabled(m, !on);
					}
				}
				GUILayout.Label(Paint(StateColor(m), StateCode(m)), skin_.CellMono, GUILayout.Width(skin_.S(ColState)));
				GUILayout.Label(Escape(m.Name) + (WillChange(m) ? Paint(UiSkin.Warning, "  ●") : ""), skin_.Cell, GUILayout.ExpandWidth(true));
				GUILayout.Label(IsBroken(m) ? "" : m.Version.ToString(), skin_.CellRight, GUILayout.Width(skin_.S(ColVersion)));
				GUILayout.EndHorizontal();
				Rect last = GUILayoutUtility.GetLastRect();
				Event e = Event.current;
				if (e.type == EventType.MouseDown && e.button == 0 && last.Contains(e.mousePosition)) {
					Select(m, i, false);
					GUIUtility.keyboardControl = 0;
					e.Use();
				}
			}
			GUILayout.EndScrollView();
			if (Event.current.type == EventType.Repaint) {
				listViewHeight_ = GUILayoutUtility.GetLastRect().height;
			}
		}

		private void Tab(Filter f, string label, int count) {
			if (GUILayout.Button(label + " " + count, filter_ == f ? skin_.TabActive : skin_.Tab, GUILayout.ExpandWidth(false))) {
				filter_ = f;
			}
		}

		// ---- a mod's panel: its own GUILayout code inside a scroll view, exceptions shown in place
		private void DrawPanel(ModInfo m, IModPanel panel) {
			if (ui_ == null || !ReferenceEquals(uiSkin_, skin_)) {
				ui_ = skin_.ToModUi(Toggle);
				uiSkin_ = skin_;
			}
			panelScroll_.TryGetValue(m.Folder, out Vector2 scroll);
			scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			panelScroll_[m.Folder] = scroll;
			GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(skin_.S(16), skin_.S(16), skin_.S(10), skin_.S(10)) });
			string key = m.Id + "/" + panel.Title;
			if (panelErrors_.TryGetValue(key, out string error)) {
				GUILayout.Label("The panel stopped: " + Escape(error), skin_.ErrorBox);
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
					BepInEx.Logging.Logger.CreateLogSource("Aether Loader").LogError(m.Id + ": panel \"" + panel.Title + "\" threw: " + ex);
				}
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
		}

		private readonly Dictionary<string, int> panelTab_ = new Dictionary<string, int>();
		private readonly Dictionary<string, Vector2> panelScroll_ = new Dictionary<string, Vector2>();
		private readonly Dictionary<string, string> panelErrors_ = new Dictionary<string, string>();
		private ModUi ui_;
		private UiSkin uiSkin_;

		// ---- editor: the selected mod
		private void DrawDetails(ModInfo m) {
			if (m == null) {
				GUILayout.Label("Select a mod.", skin_.Text);
				return;
			}
			// the mod's own panels (API 1.1): tabs beside its details
			IReadOnlyList<IModPanel> panels = m.State == ModState.Loaded ? loader_.PanelsOf(m) : Array.Empty<IModPanel>();
			panelTab_.TryGetValue(m.Folder, out int tab);
			if (tab > panels.Count) {
				tab = 0;
			}
			if (panels.Count > 0) {
				GUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(skin_.S(8), skin_.S(8), skin_.S(4), 0) });
				for (int i = 0; i <= panels.Count; ++i) {
					string title = i == 0 ? "DETAILS" : (panels[i - 1].Title ?? "PANEL").ToUpperInvariant();
					if (GUILayout.Button(title, i == tab ? skin_.TabActive : skin_.Tab, GUILayout.ExpandWidth(false))) {
						tab = i;
						panelTab_[m.Folder] = i;
					}
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Box(GUIContent.none, skin_.Divider, GUILayout.ExpandWidth(true));
			}
			if (tab > 0) {
				DrawPanel(m, panels[tab - 1]);
				return;
			}
			detailScroll_ = GUILayout.BeginScrollView(detailScroll_, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
			GUILayout.Label(Escape(m.Name), skin_.Name);
			GUILayout.Label(Escape(m.Id), skin_.Id);
			if (WillChange(m)) {
				GUILayout.Label(config_.Disabled.Contains(m.Id) ? "Disabled. It will not load after the game restarts." : "Enabled. It loads after the game restarts.", skin_.Notice);
			}
			if (IsProblem(m) && !string.IsNullOrEmpty(m.Reason)) {
				GUILayout.Label(Escape(m.Reason), skin_.ErrorBox);
			}
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
			if (!string.IsNullOrEmpty(m.Description)) {
				GUILayout.Label("DESCRIPTION", skin_.Section);
				GUILayout.Label(Escape(m.Description), skin_.Text);
			}

			GUILayout.Label("REQUIRES (" + m.Dependencies.Count + ")", skin_.Section);
			if (m.Dependencies.Count == 0) {
				GUILayout.Label(Paint(UiSkin.Secondary, "None"), skin_.Text);
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
				GUILayout.Label(Escape(d.Id) + Paint(UiSkin.Secondary, "  " + Escape(range) + (d.Optional ? "  optional" : "")) + "  " + state, skin_.TextMono);
			}

			List<ModInfo> dependents = loader_.Dependents(m.Id);
			GUILayout.Label("REQUIRED BY (" + dependents.Count + ")", skin_.Section);
			if (dependents.Count == 0) {
				GUILayout.Label(Paint(UiSkin.Secondary, "None"), skin_.Text);
			}
			foreach (ModInfo d in dependents) {
				GUILayout.Label(Escape(d.Id) + "  " + Paint(StateColor(d), StateCode(d)), skin_.TextMono);
			}
			var stranded = dependents.Where(d => !config_.Disabled.Contains(d.Id)).ToList();
			if (config_.Disabled.Contains(m.Id) && stranded.Count > 0) {
				GUILayout.Label("While this mod is disabled, these do not load either: " + string.Join(", ", stranded.Select(d => Escape(d.Name))), skin_.Notice);
			}
			GUILayout.EndScrollView();

			GUILayout.Box(GUIContent.none, skin_.Divider, GUILayout.ExpandWidth(true));
			GUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(skin_.S(16), skin_.S(16), skin_.S(10), skin_.S(10)) });
			if (!IsBroken(m)) {
				bool on = !config_.Disabled.Contains(m.Id);
				if (GUILayout.Button(on ? "Disable" : "Enable", on ? skin_.Button : skin_.ButtonPrimary, GUILayout.ExpandWidth(false))) {
					SetEnabled(m, !on);
				}
			}
			if (GUILayout.Button("Open Folder", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(m.Directory);
			}
			if (!IsBroken(m) && GUILayout.Button("Open Config", skin_.Button, GUILayout.ExpandWidth(false))) {
				OpenFolder(loader_.ConfigDirectory(m));
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private void Field(string key, string value, GUIStyle style) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(key, skin_.Key, GUILayout.Width(skin_.S(KeyWidth)));
			GUILayout.Label(value, style);
			GUILayout.EndHorizontal();
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
			switch (filter_) {
				case Filter.Ok when m.State != ModState.Loaded: return false;
				case Filter.Off when m.State != ModState.Disabled: return false;
				case Filter.Problems when !IsProblem(m): return false;
			}
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
