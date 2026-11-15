using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aether;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AetherLoader {
	/// <summary>
	/// Finds the mods (AetherLoader\mods\*\mod.json), decides which load and in what order, and loads them:
	///   1. manifests: unreadable ones and duplicate ids are skipped (of two folders with the same id, the first by name stays)
	///   2. the player's switches (loader.json) and the API version
	///   3. required dependencies: present, loadable, in the version range. Repeated until nothing changes, so a mod whose
	///      dependency is skipped is skipped too
	///   4. order: dependencies first (optional ones too when present); mods in a cycle are skipped; otherwise by id
	///   5. load: the assembly, the IAetherMod class, OnLoad. A failure skips the mods that need it
	/// Every mod ends Loaded, Disabled, Skipped or Failed with a reason, shown in the window and in logs\status.json.
	/// </summary>
	internal sealed class ModLoader : IModRegistry {
		private sealed class Entry {
			public Manifest Manifest;
			public ModInfo Info => Manifest.Info;
			public IAetherMod Instance;
			public ModContext Context;
		}

		private readonly string root_;
		private readonly ManualLogSource log_;
		private readonly LoaderConfig config_;
		private readonly GameObject host_;
		private readonly List<Entry> entries_ = new List<Entry>();
		private readonly List<Entry> loadOrder_ = new List<Entry>();
		private readonly Dictionary<string, string> assemblyFiles_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public ModLoader(string root, LoaderConfig config, ManualLogSource log, GameObject host) {
			root_ = root;
			config_ = config;
			log_ = log;
			host_ = host;
		}

		public string ModsDirectory => Path.Combine(root_, "mods");
		public string ConfigDirectory(ModInfo mod) => Path.Combine(root_, "config", mod.Id);

		/// <summary>The panels a loaded mod added to the window (API 1.1).</summary>
		public IReadOnlyList<IModPanel> PanelsOf(ModInfo mod) =>
			(IReadOnlyList<IModPanel>)entries_.FirstOrDefault(e => ReferenceEquals(e.Info, mod))?.Context?.Panels ?? Array.Empty<IModPanel>();

		/// <summary>The mods that need this one (required dependencies only).</summary>
		public List<ModInfo> Dependents(string id) =>
			entries_.Where(e => !IsDuplicate(e) && e.Info.Dependencies.Any(d => d.Id == id && !d.Optional)).Select(e => e.Info).ToList();
		public IReadOnlyList<ModInfo> All => entries_.Select(e => e.Info).ToList();
		public bool IsLoaded(string id) => Find(id)?.State == ModState.Loaded;
		public ModInfo Find(string id) => entries_.FirstOrDefault(e => e.Info.Id == id && !IsDuplicate(e))?.Info;

		public void LoadAll() {
			Directory.CreateDirectory(ModsDirectory);
			AppDomain.CurrentDomain.AssemblyResolve += Resolve;
			Discover();
			Decide();
			foreach (Entry e in loadOrder_) {
				Load(e);
			}
			int loaded = entries_.Count(e => e.Info.State == ModState.Loaded);
			log_.LogInfo(loaded + " of " + entries_.Count + " Aether mods loaded");
			foreach (Entry e in entries_.Where(e => e.Info.State != ModState.Loaded)) {
				log_.LogWarning(e.Info.Id + " (" + e.Info.Folder + "): " + e.Info.State + " - " + e.Info.Reason);
			}
			WriteStatus();
		}

		public void UnloadAll() {
			for (int i = loadOrder_.Count - 1; i >= 0; --i) {
				Entry e = loadOrder_[i];
				if (e.Info.State != ModState.Loaded) {
					continue;
				}
				try {
					e.Instance.OnUnload();
				} catch (Exception ex) {
					log_.LogError(e.Info.Id + ": OnUnload threw: " + ex);
				}
				e.Context.Assets.UnloadAll();
			}
		}

		// ---- 1. manifests
		private void Discover() {
			var seen = new Dictionary<string, Entry>(StringComparer.Ordinal);
			foreach (string dir in Directory.GetDirectories(ModsDirectory).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)) {
				if (!File.Exists(Path.Combine(dir, "mod.json"))) {
					log_.LogWarning("mods\\" + Path.GetFileName(dir) + " has no mod.json: not a mod");
					continue;
				}
				var e = new Entry { Manifest = Manifest.Read(dir) };
				entries_.Add(e);
				if (e.Manifest.Error != null) {
					Skip(e, e.Manifest.Error);
				} else if (seen.TryGetValue(e.Info.Id, out Entry first)) {
					Skip(e, "the id " + e.Info.Id + " is also used by mods\\" + first.Info.Folder + " (which is the one used)");
				} else {
					seen[e.Info.Id] = e;
				}
			}
		}

		private bool IsDuplicate(Entry e) => e.Info.State == ModState.Skipped && e.Manifest.Error == null && entries_.Any(o => o != e && o.Info.Id == e.Info.Id && entries_.IndexOf(o) < entries_.IndexOf(e));

		// ---- 2 - 4
		private void Decide() {
			var api = new ModVersion(AetherApi.Major, AetherApi.Minor, 0);
			foreach (Entry e in Candidates()) {
				if (config_.Disabled.Contains(e.Info.Id)) {
					e.Info.State = ModState.Disabled;
					e.Info.Reason = "switched off in the Aether Loader window";
				} else if (e.Manifest.Api.Major != api.Major || e.Manifest.Api.Minor > api.Minor) {
					Skip(e, "built for Aether.API " + e.Manifest.Api.Major + "." + e.Manifest.Api.Minor + "; this loader has " + AetherApi.Version);
				}
			}
			bool changed = true;
			while (changed) {
				changed = false;
				foreach (Entry e in Candidates().ToList()) {
					foreach (ModDependency d in e.Info.Dependencies.Where(d => !d.Optional)) {
						string problem = DependencyProblem(d);
						if (problem != null) {
							Skip(e, problem);
							changed = true;
							break;
						}
					}
				}
			}
			// dependencies first: depth-first over the candidates in id order
			var state = new Dictionary<Entry, int>(); // 1 visiting, 2 done
			var stack = new List<Entry>();
			foreach (Entry e in Candidates().OrderBy(e => e.Info.Id, StringComparer.Ordinal).ToList()) {
				Visit(e, state, stack);
			}
		}

		private string DependencyProblem(ModDependency d) {
			Entry dep = entries_.FirstOrDefault(o => o.Info.Id == d.Id && o.Manifest.Error == null && !IsDuplicate(o));
			if (dep == null) {
				return "needs " + d.Id + " " + d.Range + ", which is not installed";
			}
			if (!d.Range.Contains(dep.Info.Version)) {
				return "needs " + d.Id + " " + d.Range + ", installed is " + dep.Info.Version;
			}
			if (dep.Info.State == ModState.Disabled) {
				return "needs " + d.Id + ", which is switched off";
			}
			if (dep.Info.State != ModState.Pending) {
				return "needs " + d.Id + ", which is not loaded (" + dep.Info.Reason + ")";
			}
			return null;
		}

		private void Visit(Entry e, Dictionary<Entry, int> state, List<Entry> stack) {
			if (state.TryGetValue(e, out int s)) {
				if (s == 1 && e.Info.State == ModState.Pending) {
					int start = stack.IndexOf(e);
					string cycle = string.Join(" -> ", stack.Skip(start).Select(x => x.Info.Id)) + " -> " + e.Info.Id;
					foreach (Entry member in stack.Skip(start)) {
						Skip(member, "dependency cycle: " + cycle);
					}
				}
				return;
			}
			state[e] = 1;
			stack.Add(e);
			foreach (ModDependency d in e.Info.Dependencies) {
				Entry dep = Candidates().FirstOrDefault(o => o.Info.Id == d.Id);
				if (dep != null) {
					Visit(dep, state, stack);
				}
			}
			stack.RemoveAt(stack.Count - 1);
			state[e] = 2;
			if (e.Info.State == ModState.Pending) {
				loadOrder_.Add(e);
			}
		}

		private IEnumerable<Entry> Candidates() => entries_.Where(e => e.Info.State == ModState.Pending);

		// ---- 5. load
		private void Load(Entry e) {
			if (e.Info.State != ModState.Pending) {
				return; // skipped by a cycle found after it was ordered
			}
			foreach (ModDependency d in e.Info.Dependencies.Where(d => !d.Optional)) {
				if (!IsLoaded(d.Id)) {
					Skip(e, "needs " + d.Id + ", which did not load (" + Find(d.Id)?.Reason + ")");
					return;
				}
			}
			var modLog = new ModLog(e.Info.Id);
			GameObject root = new GameObject(e.Info.Id);
			root.transform.SetParent(host_.transform, false);
			string configDir = Path.Combine(root_, "config", e.Info.Id);
			try {
				Directory.CreateDirectory(configDir);
				e.Context = new ModContext(e.Info, e.Info.Directory, configDir, modLog, this, root);
				if (e.Manifest.Assembly == null) {
					e.Info.State = ModState.Loaded; // assets only: another mod uses its folder
					log_.LogInfo("loaded " + e.Info + " (assets only)");
					return;
				}
				string path = Path.Combine(e.Info.Directory, e.Manifest.Assembly);
				if (!File.Exists(path)) {
					throw new LoadException(e.Manifest.Assembly + " is not in the mod's folder");
				}
				foreach (string dll in Directory.GetFiles(e.Info.Directory, "*.dll")) {
					string name = Path.GetFileNameWithoutExtension(dll);
					if (!assemblyFiles_.ContainsKey(name)) {
						assemblyFiles_[name] = dll;
					}
				}
				Assembly assembly = Assembly.LoadFrom(path);
				Type type = FindEntry(assembly, e.Manifest.Entry);
				e.Instance = (IAetherMod)Activator.CreateInstance(type);
				e.Instance.OnLoad(e.Context);
				e.Info.State = ModState.Loaded;
				log_.LogInfo("loaded " + e.Info + " (" + type.FullName + ")");
			} catch (Exception ex) {
				Exception inner = ex is TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
				e.Info.State = ModState.Failed;
				e.Info.Reason = inner is LoadException ? inner.Message : inner.GetType().Name + ": " + inner.Message;
				log_.LogError(e.Info.Id + " failed to load: " + inner);
				e.Context?.Assets.UnloadAll();
				UnityEngine.Object.Destroy(root);
			}
		}

		private static Type FindEntry(Assembly assembly, string entry) {
			Type[] types;
			try {
				types = assembly.GetTypes();
			} catch (ReflectionTypeLoadException ex) {
				string first = ex.LoaderExceptions.FirstOrDefault(x => x != null)?.Message ?? "";
				throw new LoadException("its assembly references something missing: " + first);
			}
			if (entry != null) {
				Type named = types.FirstOrDefault(t => t.FullName == entry);
				if (named == null || !typeof(IAetherMod).IsAssignableFrom(named) || named.IsAbstract) {
					throw new LoadException("\"entry\" " + entry + " is not a class implementing Aether.IAetherMod");
				}
				return named;
			}
			var found = types.Where(t => typeof(IAetherMod).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass).ToList();
			if (found.Count != 1) {
				throw new LoadException(found.Count == 0 ? "no class implements Aether.IAetherMod" : "several classes implement Aether.IAetherMod: name one with \"entry\"");
			}
			return found[0];
		}

		private Assembly Resolve(object sender, ResolveEventArgs args) {
			string name = new AssemblyName(args.Name).Name;
			if (name == "Aether.API") {
				return typeof(ModContext).Assembly;
			}
			Assembly loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
			if (loaded != null) {
				return loaded;
			}
			return assemblyFiles_.TryGetValue(name, out string path) ? Assembly.LoadFrom(path) : null;
		}

		private static void Skip(Entry e, string reason) {
			e.Info.State = ModState.Skipped;
			e.Info.Reason = reason;
		}

		// logs\status.json: what happened this start (for tools and bug reports)
		public void WriteStatus() {
			try {
				var mods = new JArray();
				foreach (Entry e in entries_) {
					mods.Add(new JObject {
						["id"] = e.Info.Id, ["name"] = e.Info.Name, ["version"] = e.Info.Version.ToString(), ["folder"] = e.Info.Folder,
						["state"] = e.Info.State.ToString(), ["reason"] = e.Info.Reason,
						["enabledNextStart"] = !config_.Disabled.Contains(e.Info.Id),
						["loadIndex"] = loadOrder_.IndexOf(e),
					});
				}
				var status = new JObject {
					["loader"] = LoaderPlugin.Version, ["api"] = AetherApi.Version, ["time"] = DateTime.Now.ToString("o"), ["mods"] = mods,
				};
				string dir = Path.Combine(root_, "logs");
				Directory.CreateDirectory(dir);
				File.WriteAllText(Path.Combine(dir, "status.json"), status.ToString());
			} catch (Exception ex) {
				log_.LogError("status.json could not be written: " + ex.Message);
			}
		}

		private sealed class LoadException : Exception {
			public LoadException(string message) : base(message) { }
		}
	}

	/// <summary>A mod's log: BepInEx's LogOutput.log, the mod's id as the source.</summary>
	internal sealed class ModLog : IModLog {
		private readonly ManualLogSource source_;

		public ModLog(string id) {
			source_ = BepInEx.Logging.Logger.CreateLogSource(id);
		}

		public void Info(string message) => source_.LogInfo(message);
		public void Warning(string message) => source_.LogWarning(message);
		public void Error(string message) => source_.LogError(message);
		public void Error(string message, Exception exception) => source_.LogError(message + ": " + exception);
	}
}
