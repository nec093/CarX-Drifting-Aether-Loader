using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aether {
	/// <summary>
	/// AssetBundles in the mod's folder, by path relative to it ("bundles/track.bundle"; paths outside the folder are
	/// refused). Each bundle is loaded once and kept until the mod is unloaded. Bundles must be built with the game's
	/// Unity version (2023.2.22f1) and for StandaloneWindows64. Failures are logged and return null (a callback gets null):
	/// they do not throw. Call from Unity's main thread.
	/// </summary>
	public sealed class ModAssets {
		private readonly string root_;
		private readonly IModLog log_;
		private readonly Dictionary<string, AssetBundle> bundles_ = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, List<Action<AssetBundle>>> loading_ = new Dictionary<string, List<Action<AssetBundle>>>(StringComparer.OrdinalIgnoreCase);
		private bool unloaded_;

		internal ModAssets(string root, IModLog log) {
			root_ = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			log_ = log;
		}

		/// <summary>The bundles loaded so far (full paths).</summary>
		public IReadOnlyCollection<string> LoadedBundles => bundles_.Keys;

		/// <summary>The full path of a file in the mod's folder, or null (and a logged error) when the path leaves it.</summary>
		public string Resolve(string relativePath) {
			if (string.IsNullOrEmpty(relativePath)) {
				log_.Error("asset path is empty");
				return null;
			}
			string full;
			try {
				full = Path.GetFullPath(Path.Combine(root_, relativePath));
			} catch (Exception e) {
				log_.Error("asset path \"" + relativePath + "\" is not valid: " + e.Message);
				return null;
			}
			if (!full.StartsWith(root_, StringComparison.OrdinalIgnoreCase)) {
				log_.Error("asset path \"" + relativePath + "\" is outside the mod's folder");
				return null;
			}
			return full;
		}

		/// <summary>Loads (or returns the already loaded) bundle. Blocks until it is read: prefer LoadBundleAsync for large ones.</summary>
		public AssetBundle LoadBundle(string relativePath) {
			string path = Check(relativePath);
			if (path == null) {
				return null;
			}
			if (bundles_.TryGetValue(path, out AssetBundle loaded)) {
				return loaded;
			}
			AssetBundle bundle = AssetBundle.LoadFromFile(path);
			return Keep(relativePath, path, bundle);
		}

		/// <summary>Loads the bundle without blocking; done runs on the main thread with the bundle or null.</summary>
		public void LoadBundleAsync(string relativePath, Action<AssetBundle> done) {
			string path = Check(relativePath);
			if (path == null) {
				done?.Invoke(null);
				return;
			}
			if (bundles_.TryGetValue(path, out AssetBundle loaded)) {
				done?.Invoke(loaded);
				return;
			}
			if (loading_.TryGetValue(path, out var waiting)) {
				waiting.Add(done);
				return;
			}
			loading_[path] = new List<Action<AssetBundle>> { done };
			AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
			request.completed += _ => {
				var callbacks = loading_[path];
				loading_.Remove(path);
				AssetBundle bundle = unloaded_ ? null : Keep(relativePath, path, request.assetBundle);
				if (unloaded_ && request.assetBundle != null) {
					request.assetBundle.Unload(true); // the mod was unloaded meanwhile
				}
				foreach (var callback in callbacks) {
					try {
						callback?.Invoke(bundle);
					} catch (Exception e) {
						log_.Error("a LoadBundleAsync callback threw", e);
					}
				}
			};
		}

		/// <summary>One asset from a bundle (loaded first if needed), or null.</summary>
		public T LoadAsset<T>(string bundlePath, string assetName) where T : UnityEngine.Object {
			AssetBundle bundle = LoadBundle(bundlePath);
			if (bundle == null) {
				return null;
			}
			T asset = bundle.LoadAsset<T>(assetName);
			if (asset == null) {
				log_.Error("\"" + assetName + "\" (" + typeof(T).Name + ") is not in " + bundlePath);
			}
			return asset;
		}

		/// <summary>Unloads one bundle; unloadObjects also destroys what was loaded from it.</summary>
		public void UnloadBundle(string relativePath, bool unloadObjects) {
			string path = Resolve(relativePath);
			if (path != null && bundles_.TryGetValue(path, out AssetBundle bundle)) {
				bundles_.Remove(path);
				if (bundle != null) {
					bundle.Unload(unloadObjects);
				}
			}
		}

		// (the loader, after OnUnload or a failed OnLoad)
		internal void UnloadAll() {
			unloaded_ = true;
			foreach (AssetBundle bundle in bundles_.Values) {
				if (bundle != null) {
					bundle.Unload(true);
				}
			}
			bundles_.Clear();
		}

		private string Check(string relativePath) {
			if (unloaded_) {
				log_.Error("assets were requested after the mod was unloaded: " + relativePath);
				return null;
			}
			string path = Resolve(relativePath);
			if (path != null && !File.Exists(path)) {
				log_.Error("bundle not found: " + relativePath);
				return null;
			}
			return path;
		}

		private AssetBundle Keep(string relativePath, string path, AssetBundle bundle) {
			if (bundle == null) {
				log_.Error("bundle could not be loaded: " + relativePath + " (another mod may have loaded the same bundle, or it was built for another Unity version / platform)");
				return null;
			}
			bundles_[path] = bundle;
			return bundle;
		}
	}
}
