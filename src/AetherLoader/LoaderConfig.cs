using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// AetherLoader\config\loader.json:
	/// <code>
	/// { "hotkey": "F9", "uiScale": 0, "window": { "x": 80, "y": 80, "width": 1080, "height": 720, "listWidth": 240, "listCollapsed": false,
	///   "maximized": false, "opacity": 1, "fadeAway": false, "fadeOpacity": 0.45 }, "input": { "blockMouse": true, "blockKeys": true },
	///   "disabled": ["mod.id"] }
	/// </code>
	/// uiScale 0 = from the screen height (1 at 1440p); the window is kept in 1440p units. The window writes it; a change to the disabled list applies at the
	/// next start (mods are not unloaded or loaded while the game runs).
	/// Development: the file dev-open-window beside it opens the window when the game starts (to look at it without a key press).
	/// </summary>
	internal sealed class LoaderConfig {
		private readonly string path_;
		public KeyCode Hotkey = KeyCode.F9;
		public float UiScale;
		public static readonly Rect DefaultWindow = new Rect(80, 80, 1080, 720); // 1440p units
		public Rect Window = DefaultWindow;
		public float ListWidth = 240;     // the mod list's width, 1440p units
		public bool ListCollapsed;        // the mod list folded away (the selected mod's pages use the whole window)
		public bool Maximized;            // the window fills the screen (Window is kept for restoring)
		public float TextSize = 1f;       // text only: 1 normal, 1.2 large, 1.4 larger (the window's other sizes stay)
		public float Opacity = 1f;        // the window's opacity (0.3 .. 1)
		public bool FadeAway;             // fade the window while the pointer is off it
		public float FadeOpacity = 0.45f; // ... to this opacity
		public bool BlockMouse = true;    // the game gets no mouse while the pointer is on the window
		public bool BlockKeys = true;     // the game gets no keys while typing in the window or pointing at it
		public readonly HashSet<string> Disabled = new HashSet<string>(StringComparer.Ordinal);
		/// <summary>The disabled list as this start read it: what the mods' states reflect.</summary>
		public readonly HashSet<string> DisabledAtStart = new HashSet<string>(StringComparer.Ordinal);
		public bool OpenAtStart;

		public LoaderConfig(string path) {
			path_ = path;
		}

		public string Directory => Path.GetDirectoryName(path_);

		public string Load() {
			OpenAtStart = File.Exists(Path.Combine(Directory, "dev-open-window"));
			if (!File.Exists(path_)) {
				Save();
				return null;
			}
			try {
				JObject json = JObject.Parse(File.ReadAllText(path_));
				if (json["hotkey"]?.Type == JTokenType.String && Enum.TryParse((string)json["hotkey"], true, out KeyCode key)) {
					Hotkey = key;
				}
				if (json["uiScale"] != null && (json["uiScale"].Type == JTokenType.Float || json["uiScale"].Type == JTokenType.Integer)) {
					UiScale = Mathf.Clamp((float)json["uiScale"], 0f, 4f);
				}
				if (json["window"] is JObject w) {
					Window = new Rect(Number(w, "x", Window.x), Number(w, "y", Window.y), Number(w, "width", Window.width), Number(w, "height", Window.height));
					// 0.2.0's default size was small for the mods' pages: it becomes the new default
					if (Mathf.Approximately(Window.width, 860) && Mathf.Approximately(Window.height, 520)) {
						Window = new Rect(Window.x, Window.y, DefaultWindow.width, DefaultWindow.height);
					}
					ListWidth = Number(w, "listWidth", ListWidth);
					ListCollapsed = w["listCollapsed"]?.Type == JTokenType.Boolean && (bool)w["listCollapsed"];
					Maximized = w["maximized"]?.Type == JTokenType.Boolean && (bool)w["maximized"];
					TextSize = Mathf.Clamp(Number(w, "textSize", TextSize), 1f, 1.6f);
					Opacity = Mathf.Clamp(Number(w, "opacity", Opacity), 0.3f, 1f);
					FadeAway = w["fadeAway"]?.Type == JTokenType.Boolean && (bool)w["fadeAway"];
					FadeOpacity = Mathf.Clamp(Number(w, "fadeOpacity", FadeOpacity), 0.1f, 1f);
				}
				if (json["input"] is JObject inp) {
					BlockMouse = inp["blockMouse"]?.Type != JTokenType.Boolean || (bool)inp["blockMouse"];
					BlockKeys = inp["blockKeys"]?.Type != JTokenType.Boolean || (bool)inp["blockKeys"];
				}
				if (json["disabled"] is JArray list) {
					foreach (JToken id in list) {
						if (id.Type == JTokenType.String) {
							Disabled.Add((string)id);
						}
					}
				}
				return null;
			} catch (Exception e) {
				return "loader.json could not be read (" + e.Message + "): every mod is enabled for this start";
			} finally {
				DisabledAtStart.UnionWith(Disabled);
			}
		}

		public void Save() {
			var disabled = new List<string>(Disabled);
			disabled.Sort(StringComparer.Ordinal);
			var json = new JObject {
				["hotkey"] = Hotkey.ToString(),
				["uiScale"] = UiScale,
				["window"] = new JObject {
					["x"] = Mathf.Round(Window.x), ["y"] = Mathf.Round(Window.y), ["width"] = Mathf.Round(Window.width), ["height"] = Mathf.Round(Window.height),
					["listWidth"] = Mathf.Round(ListWidth), ["listCollapsed"] = ListCollapsed, ["maximized"] = Maximized,
					["textSize"] = Math.Round(TextSize, 2), ["opacity"] = Math.Round(Opacity, 2), ["fadeAway"] = FadeAway, ["fadeOpacity"] = Math.Round(FadeOpacity, 2),
				},
				["input"] = new JObject { ["blockMouse"] = BlockMouse, ["blockKeys"] = BlockKeys },
				["disabled"] = new JArray(disabled.ToArray()),
			};
			System.IO.Directory.CreateDirectory(Directory);
			string temp = path_ + ".tmp";
			File.WriteAllText(temp, json.ToString());
			if (File.Exists(path_)) {
				File.Replace(temp, path_, null);
			} else {
				File.Move(temp, path_);
			}
		}

		private static float Number(JObject o, string key, float fallback) {
			JToken t = o[key];
			return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? (float)t : fallback;
		}
	}
}
