using System;
using System.Linq;
using Confuser.Core;
using Confuser.Protections.AntiTamper;
using dnlib.DotNet;

namespace Confuser.Protections {
	public interface IAntiTamperService {
		void ExcludeMethod(ConfuserContext context, MethodDef method);
	}

	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal class AntiTamperProtection : Protection, IAntiTamperService {
		public const string _Id = "anti tamper";
		public const string _FullId = "Ki.AntiTamper";
		public const string _ServiceId = "Ki.AntiTamper";
		static readonly object HandlerKey = new object();

		public override string Name {
			get { return "Anti Tamper Protection"; }
		}

		public override string Description {
			get { return "This protection ensures the integrity of application."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Maximum; }
		}

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof(IAntiTamperService), this);
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new MDPhase(this));
		}

		public void ExcludeMethod(ConfuserContext context, MethodDef method) {
			ProtectionParameters.GetParameters(context, method).Remove(this);
		}

		static bool IsNetCoreModule(ModuleDef module) {
			var corlibRef = module.GetAssemblyRefs().FirstOrDefault(asm => asm.Name == "System.Runtime");
			return corlibRef != null || module.CorLibTypes.AssemblyRef.Name == "System.Private.CoreLib";
		}

		class InjectPhase : ProtectionPhase {
			public InjectPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper helpers injection"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				Mode mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Anti);
				if (mode == Mode.JIT && IsNetCoreModule(context.CurrentModule)) {
					// The JIT-mode runtime hooks clrjit with vtable offsets that are only stable on
					// .NET Framework; on CoreCLR (.NET Core 3+/5/6/7/8/9/10) the JIT interface layout
					// changes between minor versions, so the hook would crash at startup.
					// Fall back to the Anti mode runtime, which works by decrypting the code section.
					context.Logger.WarnFormat("Anti-Tamper mode 'jit' is not supported on .NET Core; falling back to mode 'anti' for '{0}'.", context.CurrentModule.Name);
					mode = Mode.Anti;
				}
				IModeHandler modeHandler;
				switch (mode) {
					case Mode.Normal:
						modeHandler = new NormalMode();
						break;
                    case Mode.Anti:
                        modeHandler = new AntiMode();
                        break;
                    case Mode.JIT:
						modeHandler = new JITMode();
						break;
					default:
						throw new UnreachableException();
				}
				modeHandler.HandleInject((AntiTamperProtection)Parent, context, parameters);
				context.Annotations.Set(context.CurrentModule, HandlerKey, modeHandler);
			}
		}

		class MDPhase : ProtectionPhase {
			public MDPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper metadata preparation"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				var modeHandler = context.Annotations.Get<IModeHandler>(context.CurrentModule, HandlerKey);
				modeHandler.HandleMD((AntiTamperProtection)Parent, context, parameters);
			}
		}

		enum Mode {
			Normal,
            Anti,
			JIT
		}
	}
}