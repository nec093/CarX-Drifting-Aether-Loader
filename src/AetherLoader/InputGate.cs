using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// Keeps the game from acting on what is meant for the window. The window is IMGUI, which the game's own input does
	/// not see as taken, so a click, a drag, the wheel or a typed key would also reach the game (its garage buttons, the
	/// camera, its shortcuts). While the pointer is on the window (mouse) or a field of it is being typed in (keys):
	///   - Unity's Input (the game's camera reads the mouse buttons and wheel there) answers "not pressed", "no wheel";
	///   - Rewired (the game's controls) has its mouse / keyboard switched off;
	///   - Unity's UI (EventSystem: the game's menus) is switched off, so its buttons get no click or hover;
	///   - the game's own "is the pointer on the UI" (UIUtils.IsPointerOverGameObject) answers yes.
	/// Each is found by name; a game without one of them just goes without that part. A button let go is always passed
	/// on (a drag started in the game ends there).
	/// </summary>
	internal static class InputGate {
		public static bool BlockMouse { get; private set; }
		public static bool BlockKeys { get; private set; }

		private static Harmony harmony_;
		private static PropertyInfo rewiredControllers_, rwMouse_, rwKeyboard_, rwEnabled_;
		private static Type eventSystem_;
		private static readonly List<Behaviour> systemsOff_ = new List<Behaviour>();
		private static bool rwMouseWas_ = true, rwKeysWas_ = true, rwMouseOff_, rwKeysOff_;
		private static BepInEx.Logging.ManualLogSource log_;

		public static void Install(BepInEx.Logging.ManualLogSource log) {
			log_ = log;
			harmony_ = new Harmony("nec093.aether.loader.input");
			var input = typeof(Input);
			Patch(input, "GetMouseButton", new[] { typeof(int) }, nameof(MouseHeld));
			Patch(input, "GetMouseButtonDown", new[] { typeof(int) }, nameof(MouseHeld));
			Patch(input, "get_mouseScrollDelta", Type.EmptyTypes, nameof(NoWheel));
			Patch(input, "GetKey", new[] { typeof(KeyCode) }, nameof(KeyHeld));
			Patch(input, "GetKeyDown", new[] { typeof(KeyCode) }, nameof(KeyHeld));
			Patch(input, "GetKey", new[] { typeof(string) }, nameof(KeyHeld));
			Patch(input, "GetKeyDown", new[] { typeof(string) }, nameof(KeyHeld));
			Patch(input, "get_anyKeyDown", Type.EmptyTypes, nameof(KeyHeld));
			Type ui = AccessTools.TypeByName("UIUtils");
			MethodInfo over = ui == null ? null : AccessTools.Method(ui, "IsPointerOverGameObject", Type.EmptyTypes);
			if (over != null) {
				harmony_.Patch(over, prefix: new HarmonyMethod(typeof(InputGate), nameof(PointerOverUi)));
			}
			Type re = AccessTools.TypeByName("Rewired.ReInput");
			rewiredControllers_ = re == null ? null : AccessTools.Property(re, "controllers");
			eventSystem_ = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
		}

		private static void Patch(Type type, string name, Type[] args, string prefix) {
			try {
				MethodInfo m = AccessTools.Method(type, name, args);
				if (m != null) {
					harmony_.Patch(m, prefix: new HarmonyMethod(typeof(InputGate), prefix));
				}
			} catch (Exception e) {
				log_?.LogWarning("input: " + type.Name + "." + name + " could not be held back for the window: " + e.Message);
			}
		}

		/// <summary>Called by the window every frame: what it takes this frame.</summary>
		public static void Set(bool mouse, bool keys) {
			BlockMouse = mouse;
			BlockKeys = keys;
			Rewired(mouse, keys);
			EventSystems(mouse);
		}

		// ---- Unity's Input

		private static bool MouseHeld(ref bool __result) {
			if (!BlockMouse) {
				return true;
			}
			__result = false;
			return false;
		}

		private static bool NoWheel(ref Vector2 __result) {
			if (!BlockMouse) {
				return true;
			}
			__result = Vector2.zero;
			return false;
		}

		private static bool KeyHeld(ref bool __result) {
			if (!BlockKeys) {
				return true;
			}
			__result = false;
			return false;
		}

		private static bool PointerOverUi(ref bool __result) {
			if (!BlockMouse) {
				return true;
			}
			__result = true;
			return false;
		}

		// ---- Rewired

		private static void Rewired(bool mouse, bool keys) {
			try {
				object controllers = rewiredControllers_?.GetValue(null);
				if (controllers == null) {
					return;
				}
				if (rwMouse_ == null) {
					rwMouse_ = AccessTools.Property(controllers.GetType(), "Mouse");
					rwKeyboard_ = AccessTools.Property(controllers.GetType(), "Keyboard");
				}
				Switch(rwMouse_?.GetValue(controllers), mouse, ref rwMouseOff_, ref rwMouseWas_);
				Switch(rwKeyboard_?.GetValue(controllers), keys, ref rwKeysOff_, ref rwKeysWas_);
			} catch (Exception) {
				rewiredControllers_ = null; // not this Rewired: go without it
			}
		}

		private static void Switch(object controller, bool off, ref bool isOff, ref bool was) {
			if (controller == null || off == isOff) {
				return;
			}
			if (rwEnabled_ == null) {
				rwEnabled_ = AccessTools.Property(controller.GetType(), "enabled");
			}
			if (off) {
				was = (bool)rwEnabled_.GetValue(controller);
				rwEnabled_.SetValue(controller, false);
			} else {
				rwEnabled_.SetValue(controller, was);
			}
			isOff = off;
		}

		// ---- Unity's UI

		private static void EventSystems(bool off) {
			if (eventSystem_ == null) {
				return;
			}
			if (off && systemsOff_.Count == 0) {
				foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(eventSystem_, FindObjectsSortMode.None)) {
					if (o is Behaviour b && b.enabled) {
						b.enabled = false;
						systemsOff_.Add(b);
					}
				}
			} else if (!off && systemsOff_.Count > 0) {
				foreach (Behaviour b in systemsOff_) {
					if (b != null) {
						b.enabled = true;
					}
				}
				systemsOff_.Clear();
			}
		}
	}
}
