using System;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core {
	/// <summary>
	///     The listener of module writer event.
	/// </summary>
	public class ModuleWriterListener {
		/// <summary>
		///     Raised for each writer event.
		/// </summary>
		public event EventHandler<ModuleWriterListenerEventArgs> OnWriterEvent;

		internal void Raise(ModuleWriterBase writer, ModuleWriterEvent evt) {
			if (evt == ModuleWriterEvent.PESectionsCreated)
				NativeEraser.Erase(writer as NativeModuleWriter, writer.Module as ModuleDefMD);
			OnWriterEvent?.Invoke(writer, new ModuleWriterListenerEventArgs(evt));
		}
	}

	/// <summary>
	///     Indicates the triggered writer event.
	/// </summary>
	public class ModuleWriterListenerEventArgs : EventArgs {
		/// <summary>
		///     Initializes a new instance of the <see cref="ModuleWriterListenerEventArgs" /> class.
		/// </summary>
		/// <param name="evt">The triggered writer event.</param>
		public ModuleWriterListenerEventArgs(ModuleWriterEvent evt) {
			WriterEvent = evt;
		}

		/// <summary>
		///     Gets the triggered writer event.
		/// </summary>
		/// <value>The triggered writer event.</value>
		public ModuleWriterEvent WriterEvent { get; }

		/// <summary>
		///     Converts to ModuleWriterEventArgs.
		/// </summary>
		public static implicit operator ModuleWriterEvent(ModuleWriterListenerEventArgs args) {
			return args.WriterEvent;
		}
	}
}
