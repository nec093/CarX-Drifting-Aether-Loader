using System;
using UnityEngine;

namespace Aether {
	/// <summary>
	/// A page of a mod's own in the Aether Loader window (API 1.1): the window shows the mod's panels as tabs beside its
	/// details. Draw runs inside the window's OnGUI (IMGUI, GUILayout) with the window's styles; keep it light (it runs
	/// every GUI event while the page is shown). An exception is caught and shown in place of the page.
	/// </summary>
	public interface IModPanel {
		string Title { get; }
		void Draw(ModUi ui);
	}

	/// <summary>The window's look for a mod's panel: styles (VS Code Dark+, WCAG AA contrast) and helpers.</summary>
	public sealed class ModUi {
		internal ModUi() {
		}

		/// <summary>The UI scale: multiply pixel sizes with it (or use S).</summary>
		public float Scale { get; internal set; } = 1f;
		public GUIStyle Text { get; internal set; }
		public GUIStyle TextMono { get; internal set; }
		public GUIStyle Secondary { get; internal set; }
		public GUIStyle Heading { get; internal set; }
		public GUIStyle Button { get; internal set; }
		public GUIStyle ButtonPrimary { get; internal set; }
		public GUIStyle Input { get; internal set; }
		public GUIStyle Row { get; internal set; }
		public GUIStyle RowSelected { get; internal set; }
		public GUIStyle Cell { get; internal set; }
		public GUIStyle CellMono { get; internal set; }
		public GUIStyle ErrorBox { get; internal set; }
		public GUIStyle Notice { get; internal set; }
		public GUIStyle Tab { get; internal set; }
		public GUIStyle TabActive { get; internal set; }
		public GUIStyle Divider { get; internal set; }
		/// <summary>Colours as rich text: &lt;color=#..&gt;. Ok / warning / error / secondary text.</summary>
		public string OkColor { get; internal set; }
		public string WarningColor { get; internal set; }
		public string ErrorColor { get; internal set; }
		public string SecondaryColor { get; internal set; }

		internal Action CloseAction;

		public int S(float pixels) => Mathf.RoundToInt(pixels * Scale);

		/// <summary>Closes the Aether Loader window (e.g. before a mod starts loading a scene).</summary>
		public void CloseWindow() => CloseAction?.Invoke();
	}
}
