using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aether {
	/// <summary>major.minor.patch (missing parts are 0; no pre-release tags).</summary>
	public readonly struct ModVersion : IComparable<ModVersion>, IEquatable<ModVersion> {
		public ModVersion(int major, int minor, int patch) {
			Major = major;
			Minor = minor;
			Patch = patch;
		}

		public int Major { get; }
		public int Minor { get; }
		public int Patch { get; }

		public static bool TryParse(string text, out ModVersion version) {
			version = default;
			if (string.IsNullOrWhiteSpace(text)) {
				return false;
			}
			string[] parts = text.Trim().Split('.');
			if (parts.Length > 3) {
				return false;
			}
			var n = new int[3];
			for (int i = 0; i < parts.Length; ++i) {
				if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i])) {
					return false;
				}
			}
			version = new ModVersion(n[0], n[1], n[2]);
			return true;
		}

		public int CompareTo(ModVersion other) {
			int c = Major.CompareTo(other.Major);
			if (c == 0) {
				c = Minor.CompareTo(other.Minor);
			}
			return c != 0 ? c : Patch.CompareTo(other.Patch);
		}

		public bool Equals(ModVersion other) => CompareTo(other) == 0;
		public override bool Equals(object obj) => obj is ModVersion v && Equals(v);
		public override int GetHashCode() => (Major * 397 + Minor) * 397 + Patch;
		public override string ToString() => Major + "." + Minor + "." + Patch;
		public static bool operator <(ModVersion a, ModVersion b) => a.CompareTo(b) < 0;
		public static bool operator >(ModVersion a, ModVersion b) => a.CompareTo(b) > 0;
		public static bool operator <=(ModVersion a, ModVersion b) => a.CompareTo(b) <= 0;
		public static bool operator >=(ModVersion a, ModVersion b) => a.CompareTo(b) >= 0;
	}

	/// <summary>
	/// Which versions of a dependency a mod accepts. Space-separated conditions, all must hold: "&gt;=1.2.0", "&lt;2.0.0",
	/// "&gt;1.0", "&lt;=1.4", "=1.3.0". A bare version "1.2" means "1.2 or newer with the same major" (&gt;=1.2.0 &lt;2.0.0).
	/// Empty or "*": any version.
	/// </summary>
	public sealed class VersionRange {
		private readonly List<(string op, ModVersion v)> conditions_ = new List<(string, ModVersion)>();
		private readonly string text_;

		private VersionRange(string text) {
			text_ = text;
		}

		public static VersionRange Any { get; } = new VersionRange("*");

		public static bool TryParse(string text, out VersionRange range, out string error) {
			error = null;
			range = null;
			if (string.IsNullOrWhiteSpace(text) || text.Trim() == "*") {
				range = Any;
				return true;
			}
			var r = new VersionRange(text.Trim());
			foreach (string token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
				string op = token.StartsWith(">=") || token.StartsWith("<=") ? token.Substring(0, 2) : token[0] == '>' || token[0] == '<' || token[0] == '=' ? token.Substring(0, 1) : "";
				if (!ModVersion.TryParse(token.Substring(op.Length), out ModVersion v)) {
					error = "\"" + token + "\" is not a version condition";
					return false;
				}
				if (op == "") {
					r.conditions_.Add((">=", v));
					r.conditions_.Add(("<", new ModVersion(v.Major + 1, 0, 0)));
				} else {
					r.conditions_.Add((op, v));
				}
			}
			range = r;
			return true;
		}

		public bool Contains(ModVersion version) {
			foreach (var (op, v) in conditions_) {
				bool ok = op switch {
					">=" => version >= v,
					"<=" => version <= v,
					">" => version > v,
					"<" => version < v,
					_ => version.Equals(v),
				};
				if (!ok) {
					return false;
				}
			}
			return true;
		}

		public override string ToString() => text_;
	}
}
