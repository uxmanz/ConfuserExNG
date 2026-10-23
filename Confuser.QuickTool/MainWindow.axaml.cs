using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Confuser.QuickTool {
	public partial class MainWindow : Window {
		QuickLogger logger;
		CancellationTokenSource cts;
		RunResult lastResult;

		public MainWindow() {
			InitializeComponent();
			try {
				Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Confuser.QuickTool/ConfuserExNG_32.png")));
			}
			catch {
				// The icon is cosmetic; never fail startup over it.
			}

			btnInput.Click += async (_, __) => await BrowseInput();
			btnOutput.Click += async (_, __) => await BrowseOutput();
			btnCsprojBrowse.Click += async (_, __) => await BrowseCsproj();
			btnIntegrate.Click += (_, __) => IntegrateBuild();
			btnRun.Click += (_, __) => Run();
			btnStop.Click += (_, __) => StopRun();
			btnOpenOutput.Click += (_, __) => OpenOutput();

			chkRename.IsCheckedChanged += (_, __) => cboRenameMode.IsEnabled = chkRename.IsChecked == true;
			chkCtrlFlow.IsCheckedChanged += (_, __) => nudIntensity.IsEnabled = chkCtrlFlow.IsChecked == true;
			chkAntiDebug.IsCheckedChanged += (_, __) => cboAntiDebugMode.IsEnabled = chkAntiDebug.IsChecked == true;
			chkAntiTamper.IsCheckedChanged += (_, __) => cboAntiTamperMode.IsEnabled = chkAntiTamper.IsChecked == true;
		}

		async Task BrowseInput() {
			var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
				Title = "Select the module to protect",
				AllowMultiple = false,
				FileTypeFilter = new[] {
					new Avalonia.Platform.Storage.FilePickerFileType(".NET assemblies") {
						Patterns = new[] { "*.exe", "*.dll", "*.netmodule", "*.winmd" }
					},
					new Avalonia.Platform.Storage.FilePickerFileType("All files") {
						Patterns = new[] { "*.*" }
					}
				}
			});
			var file = files.FirstOrDefault();
			if (file == null)
				return;
			txtInput.Text = file.Path.LocalPath;
			if (string.IsNullOrEmpty(txtOutput.Text))
				txtOutput.Text = Path.GetDirectoryName(file.Path.LocalPath);
			chkPdb.IsChecked = File.Exists(Path.ChangeExtension(file.Path.LocalPath, ".pdb"));
		}

		async Task BrowseOutput() {
			var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions {
				Title = "Select the output folder for the protected module",
				AllowMultiple = false
			});
			var folder = folders.FirstOrDefault();
			if (folder != null)
				txtOutput.Text = folder.Path.LocalPath;
		}

		async Task BrowseCsproj() {
			var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
				Title = "Select the project file to integrate with",
				AllowMultiple = false,
				FileTypeFilter = new[] {
					new Avalonia.Platform.Storage.FilePickerFileType("C# project files") {
						Patterns = new[] { "*.csproj" }
					},
					new Avalonia.Platform.Storage.FilePickerFileType("All files") {
						Patterns = new[] { "*.*" }
					}
				}
			});
			var file = files.FirstOrDefault();
			if (file != null)
				txtCsproj.Text = file.Path.LocalPath;
		}

		QuickOptions CollectOptions() {
			return new QuickOptions {
				Rename = chkRename.IsChecked == true,
				RenameMode = chkRename.IsChecked == true ? SelectedCombo(cboRenameMode) : null,
				CtrlFlow = chkCtrlFlow.IsChecked == true,
				Intensity = (int)(nudIntensity.Value ?? 60),
				Constants = chkConstants.IsChecked == true,
				Resources = chkResources.IsChecked == true,
				RefProxy = chkRefProxy.IsChecked == true,
				AntiDebug = chkAntiDebug.IsChecked == true,
				AntiDebugMode = chkAntiDebug.IsChecked == true ? SelectedCombo(cboAntiDebugMode) : null,
				AntiIldasm = chkAntiIldasm.IsChecked == true,
				AntiTamper = chkAntiTamper.IsChecked == true,
				AntiTamperMode = chkAntiTamper.IsChecked == true ? SelectedCombo(cboAntiTamperMode) : null,
				Integrity = chkIntegrity.IsChecked == true,
				AntiDump = chkAntiDump.IsChecked == true,
				TypeScramble = chkTypeScramble.IsChecked == true,
				LoadPdb = chkPdb.IsChecked == true,
				BackupOriginal = chkBackup.IsChecked == true,
				Seed = txtSeed.Text
			};
		}

		static string SelectedCombo(ComboBox combo) {
			if (combo.SelectedItem is ComboBoxItem item && item.Content is string value)
				return value;
			return null;
		}

		void AppendLog(string msg) {
			txtLog.Text = (txtLog.Text ?? string.Empty) + msg + Environment.NewLine;
			txtLog.CaretIndex = txtLog.Text?.Length ?? 0;
		}

		void Run() {
			string input = txtInput.Text?.Trim() ?? string.Empty;
			if (input.Length == 0 || !File.Exists(input)) {
				_ = ShowMessage(this, "Input required", "Please select an existing .exe or .dll to protect.");
				return;
			}

			string output = txtOutput.Text?.Trim();
			if (string.IsNullOrEmpty(output))
				output = Path.GetDirectoryName(Path.GetFullPath(input));

			var options = CollectOptions();
			logger = new QuickLogger();
			logger.OnOutput = msg => Dispatcher.UIThread.Post(() => AppendLog(msg));

			cts = new CancellationTokenSource();
			btnRun.IsEnabled = false;
			btnStop.IsEnabled = true;
			btnOpenOutput.IsEnabled = false;
			lastResult = null;
			lblStatus.Text = "Running...";

			var capturedInput = input;
			var capturedOutput = output;
			var capturedLogger = logger;
			var capturedToken = cts.Token;

			Task.Run(() => QuickRunner.RunAsync(capturedInput, capturedOutput, options, capturedLogger, capturedToken))
				.ContinueWith(t => Dispatcher.UIThread.Post(() => OnRunFinished(t)));
		}

		void OnRunFinished(Task<RunResult> task) {
			btnRun.IsEnabled = true;
			btnStop.IsEnabled = false;
			cts = null;

			if (task.IsFaulted) {
				var ex = task.Exception.GetBaseException();
				AppendLog("Exception: " + ex);
				lblStatus.Text = "Failed: " + ex.Message;
				_ = ShowMessage(this, "Obfuscation failed", "Unexpected error: " + ex.Message);
				return;
			}

			lastResult = task.Result;
			if (lastResult == null || string.IsNullOrEmpty(lastResult.OutputPath)) {
				lblStatus.Text = "Failed.";
				_ = ShowMessage(this, "Failed", "Obfuscation did not complete successfully; see the log for details.");
				return;
			}

			if (!string.IsNullOrEmpty(lastResult.BackupPath))
				lblStatus.Text = "Done (backup: " + Path.GetFileName(lastResult.BackupPath) + ")";
			else
				lblStatus.Text = "Done.";
			btnOpenOutput.IsEnabled = true;
		}

		void StopRun() {
			if (cts == null)
				return;
			try {
				cts.Cancel();
			}
			catch (ObjectDisposedException) {
			}
			lblStatus.Text = "Stopping...";
		}

		void OpenOutput() {
			if (lastResult == null || string.IsNullOrEmpty(lastResult.OutputPath))
				return;
			try {
				// Cross-platform: open the folder that contains the result.
				string target = File.Exists(lastResult.OutputPath)
					? Path.GetDirectoryName(lastResult.OutputPath)
					: lastResult.OutputPath;
				Process.Start(new ProcessStartInfo {
					FileName = target,
					UseShellExecute = true
				});
			}
			catch (Exception ex) {
				_ = ShowMessage(this, "Error", "Failed to open the folder: " + ex.Message);
			}
		}

		async void IntegrateBuild() {
			string projPath = txtCsproj.Text?.Trim() ?? string.Empty;
			if (projPath.Length == 0 || !File.Exists(projPath)) {
				await ShowMessage(this, "Project file required", "Please select an existing .csproj file to integrate with.");
				return;
			}

			string toolDir = AppDomain.CurrentDomain.BaseDirectory;
			string targetsSource = Path.Combine(toolDir, "ConfuserExNG.targets");
			if (!File.Exists(targetsSource)) {
				await ShowMessage(this, "Integration files missing",
					"ConfuserExNG.targets was not found next to this tool.\nRun the Quick Tool from the ConfuserExNG Release folder.");
				return;
			}

			string text;
			try {
				text = File.ReadAllText(projPath);
			}
			catch (Exception ex) {
				await ShowMessage(this, "Read error", "Cannot read '" + projPath + "':\n" + ex.Message);
				return;
			}

			if (text.IndexOf("ConfuserExNG.targets", StringComparison.OrdinalIgnoreCase) >= 0) {
				AppendLog("Project already integrates ConfuserExNG build protection: " + projPath);
				await ShowMessage(this, "Already integrated",
					"The selected project already contains the ConfuserExNG build integration. To update the protections, delete the import from the .csproj file and run this command again.");
				return;
			}

			string backupPath = projPath + ".bak";
			try {
				if (!File.Exists(backupPath))
					File.Copy(projPath, backupPath, false);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
			}

			string projectDir = Path.GetDirectoryName(Path.GetFullPath(projPath));
			string projectRules = Path.Combine(projectDir, "ConfuserExRules.crproj");
			try {
				File.WriteAllText(projectRules, CustomBuildRulesXml(CollectOptions()));
				AppendLog("Generated rule template: " + projectRules);
			}
			catch (Exception ex) {
				await ShowMessage(this, "Write error", "Cannot write the rule template '" + projectRules + "':\n" + ex.Message);
				return;
			}

			int closeIndex = text.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
			if (closeIndex < 0) {
				await ShowMessage(this, "Unsupported project", "The file does not look like a project file: no '</Project>' element was found.");
				return;
			}

			var block = new StringBuilder();
			block.AppendLine("  <PropertyGroup>");
			block.AppendLine("    <!-- ConfuserExNG build integration: obfuscates the Release output and deletes debug PDBs. -->");
			block.AppendLine("    <ConfuserExToolPath>" + EscapeXmlAttribute(toolDir) + "</ConfuserExToolPath>");
			block.AppendLine("    <ConfuserExRules>$(MSBuildProjectDirectory)\\ConfuserExRules.crproj</ConfuserExRules>");
			block.AppendLine("  </PropertyGroup>");
			block.AppendLine("  <Import Project=\"" + EscapeXmlAttribute(targetsSource) + "\" />");

			string updated = text.Substring(0, closeIndex) + block.ToString() + text.Substring(closeIndex);

			try {
				File.WriteAllText(projPath, updated);
			}
			catch (Exception ex) {
				await ShowMessage(this, "Write error", "Cannot update '" + projPath + "':\n" + ex.Message);
				return;
			}

			AppendLog("Enabled build integration in: " + projPath);
			AppendLog("Backup of the original project file: " + backupPath);
			lblStatus.Text = "Build integration enabled.";
			await ShowMessage(this, "Build integration enabled",
				"Build integration enabled.\n\nOn every Release build and single-file publish, the output of this project is obfuscated with the currently selected protections and its debug PDB is deleted.\n\nProject: " + projPath + "\nBackup: " + backupPath);
		}

		string CustomBuildRulesXml(QuickOptions opt) {
			var sb = new StringBuilder();
			sb.AppendLine("<project baseDir=\".\" outputDir=\"confex-tmp\" debug=\"false\">");
			sb.AppendLine("  <rule pattern=\"true\" inherit=\"false\">");
			if (opt.Rename) {
				if (string.IsNullOrEmpty(opt.RenameMode) || opt.RenameMode == "unicode")
					sb.AppendLine("    <protection id=\"rename\" />");
				else {
					sb.AppendLine("    <protection id=\"rename\">");
					sb.AppendLine("      <parameter name=\"mode\" value=\"" + opt.RenameMode + "\" />");
					sb.AppendLine("    </protection>");
				}
			}
			if (opt.CtrlFlow) {
				sb.AppendLine("    <protection id=\"ctrl flow\">");
				sb.AppendLine("      <parameter name=\"intensity\" value=\"" + opt.Intensity.ToString() + "\" />");
				sb.AppendLine("    </protection>");
			}
			if (opt.Constants)
				sb.AppendLine("    <protection id=\"constants\" />");
			if (opt.Resources)
				sb.AppendLine("    <protection id=\"resources\" />");
			if (opt.RefProxy)
				sb.AppendLine("    <protection id=\"ref proxy\" />");
			if (opt.AntiDebug) {
				sb.AppendLine("    <protection id=\"anti debug\">");
				sb.AppendLine("      <parameter name=\"mode\" value=\"" + opt.AntiDebugMode + "\" />");
				sb.AppendLine("    </protection>");
			}
			if (opt.AntiIldasm) {
				sb.AppendLine("    <protection id=\"anti ildasm\">");
				sb.AppendLine("      <parameter name=\"mode\" value=\"normal\" />");
				sb.AppendLine("    </protection>");
			}
			if (opt.AntiTamper) {
				sb.AppendLine("    <protection id=\"anti tamper\">");
				sb.AppendLine("      <parameter name=\"mode\" value=\"" + opt.AntiTamperMode + "\" />");
				sb.AppendLine("    </protection>");
			}
			if (opt.Integrity)
				sb.AppendLine("    <protection id=\"integrity\" />");
			if (opt.AntiDump)
				sb.AppendLine("    <protection id=\"anti dump\" />");
			if (opt.TypeScramble)
				sb.AppendLine("    <protection id=\"typescramble\" />");
			sb.AppendLine("  </rule>");
			sb.Append("</project>");
			return sb.ToString();
		}

		static string EscapeXmlAttribute(string value) {
			if (string.IsNullOrEmpty(value))
				return string.Empty;
			return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
		}

		static async Task ShowMessage(Window owner, string title, string message) {
			try {
				var dialog = new Window {
					Title = title,
					CanResize = false,
					SizeToContent = SizeToContent.WidthAndHeight,
					WindowStartupLocation = WindowStartupLocation.CenterOwner,
					MinWidth = 400,
					MaxWidth = 580
				};
				var text = new TextBlock {
					Text = message,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(18, 16, 18, 8),
					MaxWidth = 520
				};
				var ok = new Button {
					Content = "OK",
					Width = 90,
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
					Margin = new Thickness(0, 4, 0, 14)
				};
				ok.Click += (_, __) => dialog.Close();
				dialog.Content = new StackPanel { Children = { text, ok } };
				var ownerWindow = owner ?? (Application.Current.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
				await dialog.ShowDialog(ownerWindow ?? dialog);
			}
			catch {
				// Modal failures must not take the tool down; the log carries the message.
			}
		}
	}
}
