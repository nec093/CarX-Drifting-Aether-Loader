using System.IO;
using Aether;
using UnityEngine;

namespace Samples {
	/// <summary>The smallest Aether mod: logs, counts its starts in its config folder, and runs a component.</summary>
	public sealed class HelloMod : IAetherMod {
		public static HelloMod Instance { get; private set; }
		public int Starts { get; private set; }

		public void OnLoad(ModContext context) {
			Instance = this;
			string counter = Path.Combine(context.ConfigDirectory, "starts.txt");
			Starts = (File.Exists(counter) && int.TryParse(File.ReadAllText(counter), out int n) ? n : 0) + 1;
			File.WriteAllText(counter, Starts.ToString());
			context.Log.Info("hello from " + context.Info + ", start " + Starts + ", folder " + context.Directory);
			// the asset helper refuses paths outside the mod's folder
			context.Log.Info("outside path refused: " + (context.Assets.Resolve("../escape.bundle") == null));
			context.Root.AddComponent<HelloBehaviour>().Log = context.Log;
		}

		public void OnUnload() {
			Instance = null;
		}
	}

	internal sealed class HelloBehaviour : MonoBehaviour {
		public IModLog Log;
		private bool said_;

		private void Update() {
			if (!said_ && Time.frameCount > 10) {
				said_ = true;
				Log.Info("Update runs (frame " + Time.frameCount + ")");
			}
		}
	}
}
