using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class NormalMode : IEncodeMode {
		public IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key) {
			for (int i = 0; i < 0x10; i++) {
				yield return Instruction.Create(OpCodes.Ldloc, block);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldloc, block);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Ldloc, key);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Xor);
				yield return Instruction.Create(OpCodes.Stelem_I4);
			}
		}

		public uint[] Encrypt(uint[] data, int offset, uint[] key) {
			var ret = new uint[key.Length];
			for (int i = 0; i < key.Length; i++)
				ret[i] = data[i + offset] ^ key[i];
			return ret;
		}

		public object CreateDecoder(MethodDef decoder, CEContext ctx) {
			// The transform between the encoded call-site argument and the
			// decrypted-buffer ID is a randomly generated expression (random
			// operations, constants and depth per decoder), not a fixed opcode
			// shape that emulators can invert by pattern. Both directions are
			// generated together: 'expression' recovers the buffer ID from the
			// argument (emitted into the decoder), 'inverse' computes the
			// argument from the buffer ID (used at the call sites).
			var argVar = new Variable("{ARG}");
			var idVar = new Variable("{ID}");

			Func<int, int> encode = null;
			Expression idExpression = null;
			for (int attempt = 0; attempt < 10 && idExpression == null; attempt++) {
				Expression expression, inverse;
				ctx.DynCipher.GenerateExpressionPair(
					ctx.Random,
					new VariableExpression { Variable = argVar },
					new VariableExpression { Variable = idVar },
					ctx.Random.NextInt32(4, 8),
					out expression, out inverse);

				try {
					var decodeEval = new DMCodeGen(typeof(int), new[] { Tuple.Create(argVar.Name, typeof(int)) })
						.GenerateCIL(expression)
						.Compile<Func<int, int>>();
					var encodeEval = new DMCodeGen(typeof(int), new[] { Tuple.Create(idVar.Name, typeof(int)) })
						.GenerateCIL(inverse)
						.Compile<Func<int, int>>();

					// Sanity: the pair must round-trip the full ID space.
					bool ok = true;
					foreach (uint id in new[] { 0u, 1u, 2u, 0x12345678u, 0x3fffffffu, 0x40000000u, 0x7fffffffu, 0x80000000u, 0xffffffffu }) {
						if ((uint)decodeEval(unchecked((int)encodeEval(unchecked((int)id)))) != id) {
							ok = false;
							break;
						}
					}
					if (!ok)
						continue;

					encode = encodeEval;
					idExpression = expression;
				}
				catch (NotSupportedException) {
					// Regenerate with a different random structure.
				}
			}
			if (idExpression == null)
				throw new ConfuserException(null);

			var input = new Local(decoder.Module.CorLibTypes.UInt32);
			MutationHelper.ReplacePlaceholder(decoder, arg => {
				var ret = new List<Instruction>(arg);
				ret.Add(Instruction.Create(OpCodes.Stloc, input));
				var codeGen = new DecoderILCodeGen(decoder, ret, input);
				codeGen.GenerateCIL(idExpression);
				codeGen.Commit(decoder.Body);
				return ret.ToArray();
			});
			return encode;
		}

		public uint Encode(object data, CEContext ctx, uint id) {
			var encode = (Func<int, int>)data;
			return unchecked((uint)encode(unchecked((int)id)));
		}

		sealed class DecoderILCodeGen : CILCodeGen {
			readonly Local input;

			public DecoderILCodeGen(MethodDef method, IList<Instruction> instrs, Local input)
				: base(method, instrs) {
				this.input = input;
			}

			// Every generated expression refers to its input through this local,
			// which the placeholder arguments load before the generated code.
			protected override Local Var(Variable var) {
				return input;
			}
		}
	}
}
