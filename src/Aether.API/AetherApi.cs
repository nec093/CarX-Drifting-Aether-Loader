using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("AetherLoader")]

namespace Aether {
	/// <summary>The SDK's version. A mod's mod.json names the one it was built against ("api"): the loader loads it only
	/// when the major numbers match and the mod's minor is not newer than the loader's.</summary>
	public static class AetherApi {
		public const int Major = 1;
		public const int Minor = 1; // 1.1: mod panels in the loader window (ModContext.AddPanel)
		public static string Version => Major + "." + Minor;
	}

	/// <summary>
	/// An Aether mod's entry point: one public class with a public parameterless constructor, named by "entry" in
	/// mod.json (or the only implementation in the mod's assembly). OnLoad runs on Unity's main thread, after the mods it
	/// depends on; OnUnload when the game quits, in the reverse order. An exception from OnLoad marks the mod failed (its
	/// assets are unloaded, mods that depend on it are not loaded); other mods go on.
	/// </summary>
	public interface IAetherMod {
		void OnLoad(ModContext context);
		void OnUnload();
	}

	/// <summary>What the loader hands to a mod.</summary>
	public sealed class ModContext {
		internal ModContext(ModInfo info, string directory, string configDirectory, IModLog log, IModRegistry mods, GameObject root) {
			Info = info;
			Directory = directory;
			ConfigDirectory = configDirectory;
			Log = log;
			Mods = mods;
			Root = root;
			Assets = new ModAssets(directory, log);
		}

		public ModInfo Info { get; }
		/// <summary>The mod's own folder (AetherLoader\mods\&lt;folder&gt;): its assemblies, bundles and files.</summary>
		public string Directory { get; }
		/// <summary>AetherLoader\config\&lt;id&gt;: the mod's settings. Created before OnLoad; kept when the mod is updated.</summary>
		public string ConfigDirectory { get; }
		public IModLog Log { get; }
		/// <summary>AssetBundles from the mod's folder. Unloaded by the loader after OnUnload.</summary>
		public ModAssets Assets { get; }
		/// <summary>The other mods.</summary>
		public IModRegistry Mods { get; }
		/// <summary>A GameObject of the mod's own that lives as long as the game (scene changes do not destroy it): add
		/// components here for Update and coroutines.</summary>
		public GameObject Root { get; }

		internal readonly List<IModPanel> Panels = new List<IModPanel>();

		/// <summary>Adds a page of the mod's own to the Aether Loader window (API 1.1): shown as a tab beside the mod's
		/// details, in the order added.</summary>
		public void AddPanel(IModPanel panel) {
			if (panel != null && !Panels.Contains(panel)) {
				Panels.Add(panel);
			}
		}
	}

	public interface IModLog {
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Error(string message, Exception exception);
	}

	public interface IModRegistry {
		/// <summary>Every mod found, loaded or not.</summary>
		IReadOnlyList<ModInfo> All { get; }
		bool IsLoaded(string id);
		/// <summary>The mod with this id, or null.</summary>
		ModInfo Find(string id);
	}

	public enum ModState {
		/// <summary>Found, not processed yet.</summary>
		Pending,
		Loaded,
		/// <summary>Switched off by the player (AetherLoader\config\loader.json).</summary>
		Disabled,
		/// <summary>Not loaded: see ModInfo.Reason (manifest error, missing dependency, API version, cycle...).</summary>
		Skipped,
		/// <summary>Its assembly or OnLoad threw: see ModInfo.Reason.</summary>
		Failed,
	}

	public sealed class ModDependency {
		public ModDependency(string id, VersionRange range, bool optional) {
			Id = id;
			Range = range;
			Optional = optional;
		}

		public string Id { get; }
		public VersionRange Range { get; }
		/// <summary>An optional dependency only orders the loading: the mod loads without it.</summary>
		public bool Optional { get; }
		public override string ToString() => Id + " " + Range + (Optional ? " (optional)" : "");
	}

	public sealed class ModInfo {
		internal ModInfo(string id, string name, ModVersion version, string author, string description, string directory) {
			Id = id;
			Name = name;
			Version = version;
			Author = author;
			Description = description;
			Directory = directory;
			Folder = System.IO.Path.GetFileName(directory);
		}

		/// <summary>Unique, lower case, dots between parts: "author.mod" (e.g. "nec093.aether.map.tsukuba").</summary>
		public string Id { get; }
		public string Name { get; }
		public ModVersion Version { get; }
		public string Author { get; }
		public string Description { get; }
		/// <summary>The folder name under AetherLoader\mods.</summary>
		public string Folder { get; }
		/// <summary>The mod's folder (full path). A mod without code (assets only, "assembly" left out of mod.json) is
		/// used by another mod through this: e.g. a car parts pack read by the car parts mod.</summary>
		public string Directory { get; }
		/// <summary>The mod's mod.json, for fields of its own that other mods read (e.g. "carParts": {...}).</summary>
		public string ManifestPath => System.IO.Path.Combine(Directory, "mod.json");
		public IReadOnlyList<ModDependency> Dependencies { get; internal set; } = Array.Empty<ModDependency>();
		public ModState State { get; internal set; }
		/// <summary>Why the mod is not loaded (empty when it is).</summary>
		public string Reason { get; internal set; } = "";
		public override string ToString() => Id + " " + Version;
	}
}
