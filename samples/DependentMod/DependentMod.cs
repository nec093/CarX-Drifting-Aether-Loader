using Aether;

namespace Samples {
	/// <summary>Loads after the Hello sample and reads its state directly.</summary>
	public sealed class DependentMod : IAetherMod {
		public void OnLoad(ModContext context) {
			context.Log.Info("Hello is loaded: " + context.Mods.IsLoaded("nec093.sample.hello") + ", its start count " + HelloMod.Instance.Starts +
				", the optional dependency is installed: " + (context.Mods.Find("nec093.sample.not-installed") != null));
		}

		public void OnUnload() {
		}
	}
}
