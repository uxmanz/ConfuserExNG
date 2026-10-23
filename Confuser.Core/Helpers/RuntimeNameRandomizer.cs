using System;
using System.Collections.Generic;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Core.Helpers {
	/// <summary>
	/// Renames the embedded Confuser.Runtime scaffolding (anti-debug, anti-dump,
	/// anti-tamper, constants decryption, etc.) inside protected modules.
	/// </summary>
	/// <remarks>
	/// The runtime components were injected under their fixed source names
	/// (<c>Confuser.Runtime.AntiDebugWin32</c>, <c>Confuser.Runtime.Constant</c>,
	/// ...), so the well-known namespace string was present in every protected
	/// assembly and served as a trivial static indicator. The runtime code is
	/// self-contained and never reflects on its own member names, and by the
	/// time this pass runs (right before the module is written) every protection
	/// has finished and resolves members through object references, so the
	/// renames are behavior-neutral.
	/// </remarks>
	internal static class RuntimeNameRandomizer {
		const string RuntimeNamespace = "Confuser.Runtime";

		public static void Randomize(ModuleDef module, RandomGenerator random) {
			if (module == null || random == null)
				return;

			bool present = false;
			foreach (var type in module.Types) {
				if (type.Namespace != RuntimeNamespace)
					continue;
				present = true;
				break;
			}
			if (!present)
				return;

			string ns = UniqueName(random, null);
			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var type in module.Types) {
				if (type.Namespace != RuntimeNamespace)
					continue;
				type.Namespace = ns;
				type.Name = UniqueName(random, used);
				foreach (var method in type.Methods) {
					if (method.Name == ".ctor" || method.Name == ".cctor")
						continue;
					method.Name = UniqueName(random, used);
				}
				foreach (var field in type.Fields)
					field.Name = UniqueName(random, used);
			}
		}

		static string UniqueName(RandomGenerator random, HashSet<string> used) {
			const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
			string ret;
			do {
				var buf = new char[12];
				for (int i = 0; i < buf.Length; i++)
					buf[i] = chars[random.NextInt32(chars.Length)];
				ret = new string(buf);
			} while (used != null && !used.Add(ret));
			return ret;
		}
	}
}
