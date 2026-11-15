using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// AetherLoader\config\loader.json:
	/// <code>
	/// { "hotkey": "F9", "uiScale": 0, "window": { "x": 80, "y": 80, "width": 860, "height": 520 }, "disabled": ["mod.id"] }
	/// </code>
	/// uiScale 0 = from the screen height (1 at 1440p); the window is kept in 1440p units. The window writes it; a change to the disabled list applies at the
	/// next start (mods are not unloaded or loaded while the game runs).
	/// Development: the file dev-open-window beside it opens the window when the game starts (to look at it without a key press).
	/// </summary>
	internal sealed class LoaderConfig {
		private readonly string path_;
		public KeyCode Hotkey = KeyCode.F9;
		public float UiScale;
		public Rect Window = new Rect(80, 80, 860, 520); // 1440p units
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
				["window"] = new JObject { ["x"] = Mathf.Round(Window.x), ["y"] = Mathf.Round(Window.y), ["width"] = Mathf.Round(Window.width), ["height"] = Mathf.Round(Window.height) },
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
