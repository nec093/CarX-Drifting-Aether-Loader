using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Aether;
using Newtonsoft.Json.Linq;

namespace AetherLoader {
	/// <summary>
	/// mod.json in a mod's folder:
	/// <code>
	/// {
	///   "id": "nec093.aether.map.tsukuba",      required: lower case, parts separated by . _ -
	///   "name": "Tsukuba",                      required
	///   "version": "1.0.0",                     required: major.minor.patch
	///   "api": "1.0",                           required: the Aether.API version the mod was built against
	///   "author": "nec093", "description": "...",
	///   "assembly": "TsukubaMap.dll",           the mod's code (left out: an assets-only mod)
	///   "entry": "Tsukuba.TsukubaMod",          the IAetherMod class (left out: the only one in the assembly)
	///   "dependencies": [ { "id": "nec093.aether.maps", "version": ">=1.0.0", "optional": false } ]
	/// }
	/// </code>
	/// Fields the loader does not know are kept for the mods that read them (ModInfo.ManifestPath).
	/// </summary>
	internal sealed class Manifest {
		private static readonly Regex IdPattern = new Regex("^[a-z0-9]+([._-][a-z0-9]+)*$");

		public ModInfo Info;
		public ModVersion Api;
		public string Assembly;
		public string Entry;
		public string Error; // set: the manifest is unusable

		public static Manifest Read(string directory) {
			var m = new Manifest();
			string path = Path.Combine(directory, "mod.json");
			JObject json;
			try {
				json = JObject.Parse(File.ReadAllText(path));
			} catch (Exception e) {
				return Broken(directory, "mod.json could not be read: " + e.Message);
			}
			string id = Text(json, "id");
			string name = Text(json, "name");
			if (id == null || !IdPattern.IsMatch(id)) {
				return Broken(directory, id == null ? "mod.json has no \"id\"" : "the id \"" + id + "\" is not lower case parts separated by . _ -");
			}
			if (!ModVersion.TryParse(Text(json, "version"), out ModVersion version)) {
				return Broken(directory, "\"version\" is missing or not major.minor.patch", id);
			}
			m.Info = new ModInfo(id, string.IsNullOrEmpty(name) ? id : name, version, Text(json, "author") ?? "", Text(json, "description") ?? "", directory);
			if (!ModVersion.TryParse(Text(json, "api"), out m.Api)) {
				m.Error = "\"api\" is missing or not a version (the Aether.API version the mod was built against, e.g. \"" + AetherApi.Version + "\")";
				return m;
			}
			m.Assembly = Text(json, "assembly");
			m.Entry = Text(json, "entry");
			if (m.Assembly != null && (m.Assembly.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !m.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) {
				m.Error = "\"assembly\" must be a .dll file name in the mod's folder";
				return m;
			}
			var dependencies = new List<ModDependency>();
			JToken deps = json["dependencies"];
			if (deps != null && deps.Type != JTokenType.Array) {
				m.Error = "\"dependencies\" must be a list";
				return m;
			}
			foreach (JToken d in (JArray)deps ?? new JArray()) {
				string depId = d.Type == JTokenType.Object ? Text((JObject)d, "id") : null;
				if (depId == null) {
					m.Error = "a dependency has no \"id\"";
					return m;
				}
				if (!VersionRange.TryParse(Text((JObject)d, "version"), out VersionRange range, out string rangeError)) {
					m.Error = "dependency " + depId + ": " + rangeError;
					return m;
				}
				dependencies.Add(new ModDependency(depId, range, d["optional"]?.Type == JTokenType.Boolean && (bool)d["optional"]));
			}
			m.Info.Dependencies = dependencies;
			return m;
		}

		private static Manifest Broken(string directory, string error, string id = null) {
			// (an id is still needed for the list: the folder name stands in)
			string fallback = id ?? Path.GetFileName(directory).ToLowerInvariant();
			return new Manifest { Info = new ModInfo(fallback, Path.GetFileName(directory), default, "", "", directory), Error = error };
		}

		private static string Text(JObject json, string key) {
			JToken t = json[key];
			return t != null && t.Type == JTokenType.String ? ((string)t).Trim() : null;
		}
	}
}
