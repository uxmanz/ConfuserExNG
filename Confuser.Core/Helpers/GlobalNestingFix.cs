using System;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     Fixes the nesting of injected runtime types under the module global type.
	/// </summary>
	/// <remarks>
	///     The .NET 9+ CoreCLR rejects types nested under the module global (&lt;Module&gt;) type on
	///     assembly load with a BadImageFormatException ("Enclosing type(s) not found", see
	///     dotnet/runtime issue #111164) even though it is representable in the metadata format.
	///     This protection is only tolerated when the enclosing type is an ordinary type, so all
	///     types injected into the global type are moved into a hidden, ordinary top-level type
	///     before the module is written. Members defined directly on the global type (fields and
	///     global .cctor) are valid per ECMA-335 and are left in place.
	/// </remarks>
	public static class GlobalNestingFix {
		/// <summary>
		///     Moves all types nested directly under the module global type into a hidden top-level
		///     container type, preserving their relative nesting.
		/// </summary>
		/// <param name="module">The module to fix.</param>
		public static void Fix(ModuleDef module) {
			var global = module.GlobalType;
			if (global == null || global.NestedTypes.Count == 0)
				return;

			var mvid = (module.Mvid ?? Guid.NewGuid()).ToString("N");
			// Compiler-anonymous-style name: angle-bracketed names are hidden
			// from most tooling, and the random body avoids carrying a fixed
			// tool-identifying string in every protected assembly.
			string name = "<" + mvid.Substring(0, 12) + ">";
			for (int suffix = 1; module.GetTypes().Any(t => t.Namespace == "" && t.Name == name); suffix++)
				name = "<" + mvid.Substring(0, 12) + "_" + suffix + ">";

			var container = new TypeDefUser("", name, module.CorLibTypes.Object.TypeDefOrRef);

			container.Attributes = TypeAttributes.NotPublic | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed;
			module.Types.Add(container);

			foreach (var nested in global.NestedTypes.ToList()) {
				// CoreCLR rejects loading nested types with private/compiler-controlled visibility
				// inside a private container with a TypeLoadException ("Access is denied").
				// The injected runtime types are assembly-internal plumbing, so any restrictive
				// visibility is widened to NestedAssembly.
				const TypeAttributes VisibilityMask = (TypeAttributes)0x00000007;
				var visibility = nested.Attributes & VisibilityMask;
				if (visibility != TypeAttributes.NestedPublic && visibility != TypeAttributes.NestedAssembly && visibility != TypeAttributes.NestedFamORAssem)
					nested.Attributes = (nested.Attributes & ~VisibilityMask) | TypeAttributes.NestedAssembly;
				nested.DeclaringType = container;
			}
		}
	}
}
