using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// Aether Loader as a BepInEx plugin. Its own files: [game]\AetherLoader\
	///   mods\&lt;folder&gt;\mod.json   the mods (one folder each)
	///   config\loader.json         the window's switches; config\&lt;mod id&gt;\ each mod's settings
	///   logs\status.json           what loaded this start, and why the others did not
	/// Mods load in Awake (BepInEx's chainloader, on Unity's main thread) and unload when the game quits.
	/// </summary>
	[BepInPlugin("nec093.aether.loader", "Aether Loader", Version)]
	public sealed class LoaderPlugin : BaseUnityPlugin {
		public const string Version = "0.2.0";

		private ModLoader loader_;

		private void Awake() {
			string root = Path.Combine(Paths.GameRootPath, "AetherLoader");
			var config = new LoaderConfig(Path.Combine(root, "config", "loader.json"));
			string configError = config.Load();
			if (configError != null) {
				Logger.LogError(configError);
			}
			// the loader's own object: BepInEx's manager object is not relied on (some games destroy it)
			var host = new GameObject("Aether Loader");
			DontDestroyOnLoad(host);
			host.hideFlags = HideFlags.HideAndDontSave;
			loader_ = new ModLoader(root, config, Logger, host);
			var window = host.AddComponent<ManagerWindow>();
			window.Init(loader_, config);
			try {
				loader_.LoadAll();
			} catch (Exception e) {
				Logger.LogError("Aether Loader stopped while loading mods: " + e);
			}
			Application.quitting += () => loader_.UnloadAll();
			Logger.LogInfo("Aether Loader " + Version + " (Aether.API " + Aether.AetherApi.Version + "): window " + config.Hotkey + ", mods in " + loader_.ModsDirectory);
		}
	}
}
