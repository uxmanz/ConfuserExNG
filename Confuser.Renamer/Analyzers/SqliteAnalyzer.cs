using System;
using System.Collections.Generic;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.Analyzers {
	/// <summary>
	/// Keeps the public members of model classes used with sqlite-net
	/// (SQLite-net / sqlite-net-pcl) from being renamed.
	/// </summary>
	/// <remarks>
	/// sqlite-net maps table columns to public properties and fields by member
	/// name. Renaming those members silently breaks data binding: queries return
	/// rows whose properties are all <c>null</c>, which typically surfaces later
	/// as a <c>NullReferenceException</c> far away from the database code.
	/// Members whose column mapping does not depend on their name are still
	/// renamable: <c>[SQLite.Column("name")]</c> overrides the column name and
	/// <c>[SQLite.Ignore]</c> excludes the member from mapping entirely.
	/// </remarks>
	internal class SqliteAnalyzer : IRenamer {
		const string SqliteNamespace = "SQLite";
		const string ColumnAttribute = "SQLite.ColumnAttribute";
		const string IgnoreAttribute = "SQLite.IgnoreAttribute";

		public void Analyze(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			var method = def as MethodDef;
			if (method?.Body == null)
				return;

			foreach (var instr in method.Body.Instructions) {
				var called = instr.Operand as IMethod;
				if (called == null || !IsSqliteApi(called))
					continue;
				foreach (var type in GetReferencedTypes(called))
					MarkModelType(service, type);
			}
		}

		static bool IsSqliteApi(IMethod method) {
			var decl = method.DeclaringType;
			while (decl != null) {
				if (decl.Namespace == SqliteNamespace)
					return true;
				decl = decl.DeclaringType;
			}
			return false;
		}

		/// <summary>
		/// Collects the model types a sqlite-net call is generic over, e.g.
		/// <c>SQLiteCommand.ExecuteQuery&lt;TModel&gt;</c> or
		/// <c>TableQuery&lt;TModel&gt;.Where</c>.
		/// </summary>
		static IEnumerable<TypeDef> GetReferencedTypes(IMethod method) {
			var args = new List<TypeSig>();
			if (method is MethodSpec spec && spec.GenericInstMethodSig != null)
				args.AddRange(spec.GenericInstMethodSig.GenericArguments);
			if (method.DeclaringType is TypeSpec declSpec && declSpec.TypeSig is GenericInstSig declInst)
				args.AddRange(declInst.GenericArguments);

			foreach (var arg in args)
				foreach (var type in WalkTypeSig(arg))
					yield return type;
		}

		static IEnumerable<TypeDef> WalkTypeSig(TypeSig sig) {
			while (sig != null) {
				var def = sig.TryGetTypeDef();
				if (def != null)
					yield return def;
				if (sig is GenericInstSig inst) {
					foreach (var arg in inst.GenericArguments)
						foreach (var type in WalkTypeSig(arg))
							yield return type;
					yield break;
				}
				sig = sig.Next;
			}
		}

		static void MarkModelType(INameService service, TypeDef type) {
			if (type == null)
				return;
			service.SetCanRename(type, false);

			foreach (var prop in type.Properties) {
				if (!IsPublicInstance(prop))
					continue;
				if (NameIsIndependentOfMemberName(prop))
					continue;
				service.SetCanRename(prop, false);
			}
			foreach (var field in type.Fields) {
				if (!field.IsPublic || field.IsStatic || field.IsLiteral)
					continue;
				if (NameIsIndependentOfMemberName(field))
					continue;
				service.SetCanRename(field, false);
			}
		}

		static bool IsPublicInstance(PropertyDef prop) {
			if (prop.GetMethod != null && prop.GetMethod.IsPublic && !prop.GetMethod.IsStatic)
				return true;
			if (prop.SetMethod != null && prop.SetMethod.IsPublic && !prop.SetMethod.IsStatic)
				return true;
			return false;
		}

		static bool NameIsIndependentOfMemberName(IHasCustomAttribute def) {
			if (def.CustomAttributes.IsDefined(IgnoreAttribute))
				return true;
			foreach (var attr in def.CustomAttributes) {
				if (attr.TypeFullName != ColumnAttribute)
					continue;
				if (attr.HasConstructorArguments && attr.ConstructorArguments.Count > 0)
					return true;
				if (attr.GetProperty("PropertyName") != null)
					return true;
			}
			return false;
		}

		public void PreRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}
	}
}
