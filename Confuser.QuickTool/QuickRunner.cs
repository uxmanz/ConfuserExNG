using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;

namespace Confuser.QuickTool {
	public class QuickOptions {
		public bool Rename;
		public string RenameMode = "unicode";
		public bool CtrlFlow;
		public int Intensity = 60;
		public bool Constants;
		public bool Resources;
		public bool RefProxy;
		public bool AntiDebug;
		public string AntiDebugMode = "win32";
		public bool AntiIldasm;
		public bool AntiTamper;
		public string AntiTamperMode = "anti";
		public bool Integrity;
		public bool AntiDump;
		public bool TypeScramble;
		public bool LoadPdb;
		public bool BackupOriginal = true;
		public string Seed;
	}

	public class QuickLogger : ILogger {
		public Action<string> OnOutput;

		public bool Success { get; private set; }

		public void Debug(string msg) {
			Notify("[DEBUG] " + msg);
		}

		public void DebugFormat(string format, params object[] args) {
			Notify("[DEBUG] " + string.Format(format, args));
		}

		public void Info(string msg) {
			Notify("  [INFO] " + msg);
		}

		public void InfoFormat(string format, params object[] args) {
			Notify("  [INFO] " + string.Format(format, args));
		}

		public void Warn(string msg) {
			Notify("  [WARN] " + msg);
		}

		public void WarnFormat(string format, params object[] args) {
			Notify("  [WARN] " + string.Format(format, args));
		}

		public void WarnException(string msg, Exception ex) {
			Notify("  [WARN] " + msg);
			Notify("Exception: " + ex);
		}

		public void Error(string msg) {
			Notify("[ERROR] " + msg);
		}

		public void ErrorFormat(string format, params object[] args) {
			Notify("[ERROR] " + string.Format(format, args));
		}

		public void ErrorException(string msg, Exception ex) {
			Notify("[ERROR] " + msg);
			Notify("Exception: " + ex);
		}

		public void Progress(int progress, int overall) { }

		public void EndProgress() { }

		public void Finish(bool successful) {
			Success = successful;
			Notify(successful ? "=== Finished successfully ===" : "=== Finished with errors ===");
		}

		void Notify(string msg) {
			if (OnOutput != null)
				OnOutput(msg);
		}
	}

	public static class QuickRunner {
		public static QuickOptions DefaultOptions() {
			return new QuickOptions {
				Rename = true,
				CtrlFlow = true,
				Constants = true,
				RefProxy = true,
				AntiIldasm = true
			};
		}

		public static void BuildRule(QuickOptions options, Rule rule) {
			if (options.Rename) {
				var item = new SettingItem<Protection>("rename");
				if (!string.IsNullOrEmpty(options.RenameMode))
					item.Add("mode", options.RenameMode);
				rule.Add(item);
			}
			if (options.CtrlFlow) {
				var item = new SettingItem<Protection>("ctrl flow");
				item.Add("intensity", options.Intensity.ToString());
				rule.Add(item);
			}
			if (options.Constants)
				rule.Add(new SettingItem<Protection>("constants"));
			if (options.Resources)
				rule.Add(new SettingItem<Protection>("resources"));
			if (options.RefProxy)
				rule.Add(new SettingItem<Protection>("ref proxy"));
			if (options.AntiDebug) {
				var item = new SettingItem<Protection>("anti debug");
				item.Add("mode", options.AntiDebugMode);
				rule.Add(item);
			}
			if (options.AntiIldasm)
				rule.Add(new SettingItem<Protection>("anti ildasm"));
			if (options.AntiTamper) {
				var item = new SettingItem<Protection>("anti tamper");
				item.Add("mode", options.AntiTamperMode);
				rule.Add(item);
			}
			if (options.Integrity)
				rule.Add(new SettingItem<Protection>("integrity"));
			if (options.AntiDump)
				rule.Add(new SettingItem<Protection>("anti dump"));
			if (options.TypeScramble)
				rule.Add(new SettingItem<Protection>("typescramble"));
		}

		/// <summary>
		/// Runs the obfuscation of one module. If the output directory equals the directory
		/// of the input module, the protected file replaces the original (with automatic
		/// backup when enabled); otherwise the result is written to the output directory.
		/// Returns the path of the final output file, or null on failure.
		/// </summary>
		public static async Task<RunResult> RunAsync(string inputPath, string outputDir, QuickOptions options, QuickLogger logger, CancellationToken token) {
			var result = new RunResult();
			inputPath = Path.GetFullPath(inputPath);
			string inputDir = Path.GetDirectoryName(inputPath);
			string fileName = Path.GetFileName(inputPath);

			if (string.IsNullOrEmpty(outputDir))
				outputDir = inputDir;
			outputDir = Path.GetFullPath(outputDir);

			bool inPlace = string.Equals(
				Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar),
				inputDir.TrimEnd(Path.DirectorySeparatorChar),
				StringComparison.OrdinalIgnoreCase);

			string workDir;
			if (inPlace) {
				workDir = Path.Combine(inputDir, ".quickout");
				if (Directory.Exists(workDir))
					Directory.Delete(workDir, true);
				Directory.CreateDirectory(workDir);
			}
			else {
				workDir = outputDir;
				if (!Directory.Exists(workDir))
					Directory.CreateDirectory(workDir);
			}

			var proj = new ConfuserProject();
			proj.BaseDirectory = inputDir;
			proj.OutputDirectory = workDir;
			if (!string.IsNullOrEmpty(options.Seed))
				proj.Seed = options.Seed;
			proj.Debug = options.LoadPdb;
			proj.Add(new ProjectModule { Path = fileName });

			var rule = new Rule("true", ProtectionPreset.None, false);
			BuildRule(options, rule);
			proj.Rules.Add(rule);

			var parameters = new ConfuserParameters();
			parameters.Project = proj;
			parameters.Logger = logger;

			try {
				await ConfuserEngine.Run(parameters, token);
			}
			catch (Exception ex) {
				logger.ErrorException("Obfuscation failed.", ex);
			}

			if (!logger.Success) {
				TryCleanup(workDir);
				return result;
			}

			string produced = Path.Combine(workDir, fileName);
			if (!File.Exists(produced)) {
				logger.ErrorFormat("The obfuscated file '{0}' was not produced.", produced);
				TryCleanup(workDir);
				return result;
			}
			if (inPlace) {
				if (options.BackupOriginal) {
					string backupPath = BackupOriginal(inputPath, logger);
					if (!string.IsNullOrEmpty(backupPath))
						result.BackupPath = backupPath;
				}
				File.Copy(produced, inputPath, true);
				result.OutputPath = inputPath;
				TryCleanup(workDir);
			}
			else {
				result.OutputPath = produced;
			}
			return result;
		}

		/// <summary>
		/// Creates a backup next to the original file before it is replaced.
		/// </summary>
		public static string BackupOriginal(string inputPath, QuickLogger logger) {
			try {
				string dir = Path.GetDirectoryName(inputPath);
				string ext = Path.GetExtension(inputPath);
				string stem = Path.GetFileNameWithoutExtension(inputPath);
				string baseName = stem.EndsWith("_orig", StringComparison.OrdinalIgnoreCase)
					? stem + "_backup"
					: stem + "_orig";
				string candidate = Path.Combine(dir, baseName + ext);
				for (int i = 2; File.Exists(candidate); i++)
					candidate = Path.Combine(dir, baseName + "_" + i + ext);
				File.Copy(inputPath, candidate);
				logger.InfoFormat("Backup of the original saved to '{0}'.", candidate);
				return candidate;
			}
			catch (Exception ex) {
				logger.WarnException("Failed to create a backup of the original file.", ex);
				return null;
			}
		}

		static void TryCleanup(string dir) {
			try {
				if (Directory.Exists(dir))
					Directory.Delete(dir, true);
			}
			catch (Exception) {
			}
		}
	}

	public class RunResult {
		public string OutputPath;

		public string BackupPath;
	}
}
