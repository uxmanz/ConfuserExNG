using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;

namespace Confuser.Core.Helpers {
	/// <summary>
	/// Removes debug scopes that reference instructions which no longer exist in
	/// the method body.
	/// </summary>
	/// <remarks>
	/// Protections that rebuild method bodies (e.g. control flow mangling) can
	/// drop or replace instructions. PDB scope boundaries reference instructions
	/// by identity; when a boundary no longer resolves, the portable PDB writer
	/// aborts the whole write with
	/// <c>ModuleWriterException: Couldn't find an instruction, maybe it was removed</c>
	/// (dnlib SerializerMethodContext.GetOffset). The sanitizer drops exactly the
	/// scopes whose boundaries are gone, so writing debug symbols always succeeds.
	/// </remarks>
	internal static class PdbScopeFix {
		/// <summary>
		/// Sanitizes the PDB scopes of all methods in the module.
		/// </summary>
		public static void Fix(ModuleDef module) {
			if (module is null)
				return;
			foreach (var type in module.GetTypes()) {
				foreach (var method in type.Methods) {
					var body = method.Body;
					if (body?.PdbMethod?.Scope is null)
						continue;
					var index = BuildIndex(body);
					var root = body.PdbMethod.Scope;
					if (!WithinBody(root, index)) {
						// The writer cannot represent a root scope with broken
						// boundaries; drop all scope debug info of this method.
						body.PdbMethod = null;
						continue;
					}
					RemoveBrokenScopes(root.Scopes, index);
				}
			}
		}

		static Dictionary<Instruction, int> BuildIndex(CilBody body) {
			var index = new Dictionary<Instruction, int>(body.Instructions.Count);
			for (int i = 0; i < body.Instructions.Count; i++)
				index[body.Instructions[i]] = i;
			return index;
		}

		static bool WithinBody(PdbScope scope, Dictionary<Instruction, int> index) {
			if (scope.Start != null && !index.ContainsKey(scope.Start))
				return false;
			if (scope.End != null && !index.ContainsKey(scope.End))
				return false;
			// A scope whose start sits at or after its end would be written with
			// an underflowing length; drop it as well.
			if (scope.Start != null && scope.End != null
				&& index[scope.Start] >= index[scope.End])
				return false;
			return true;
		}

		static void RemoveBrokenScopes(IList<PdbScope> scopes, Dictionary<Instruction, int> index) {
			for (int i = scopes.Count - 1; i >= 0; i--) {
				if (WithinBody(scopes[i], index))
					RemoveBrokenScopes(scopes[i].Scopes, index);
				else
					scopes.RemoveAt(i);
			}
		}
	}
}
