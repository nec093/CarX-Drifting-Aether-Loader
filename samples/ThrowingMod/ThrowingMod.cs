using System;
using Aether;

namespace Samples {
	/// <summary>A test mod: OnLoad throws. The loader marks it failed; the other mods still load.</summary>
	public sealed class ThrowingMod : IAetherMod {
		public void OnLoad(ModContext context) {
			throw new InvalidOperationException("thrown on purpose by the test mod");
		}

		public void OnUnload() {
		}
	}
}
