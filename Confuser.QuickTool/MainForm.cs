using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Confuser.QuickTool {
	public class MainForm : Form {
		TextBox txtInput;
		TextBox txtOutput;
		CheckBox chkPdb;
		CheckBox chkBackup;
		CheckBox chkRename;
		ComboBox cboRenameMode;
		CheckBox chkCtrlFlow;
		NumericUpDown nudIntensity;
		CheckBox chkConstants;
		CheckBox chkResources;
		CheckBox chkRefProxy;
		CheckBox chkAntiDebug;
		ComboBox cboAntiDebugMode;
		CheckBox chkAntiIldasm;
		CheckBox chkAntiTamper;
		ComboBox cboAntiTamperMode;
		CheckBox chkIntegrity;
		CheckBox chkAntiDump;
		CheckBox chkTypeScramble;
		TextBox txtSeed;
		TextBox txtLog;
		Button btnRun;
		Button btnStop;
		Button btnOpenOutput;
		Label lblStatus;

		QuickLogger logger;
		CancellationTokenSource cts;
		RunResult lastResult;

		public MainForm() {
			InitializeComponent();
		}

		void InitializeComponent() {
			Text = "ConfuserEx Quick Tool";
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(660, 690);
			ClientSize = new Size(644, 798);
			Font = new Font("Tahoma", 8.25F);

			var grpSource = new GroupBox {
				Text = " Input module ",
				Location = new Point(12, 12),
				Size = new Size(ContentWidth1(), 96),
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			var lblInput = new Label {
				Text = "File to protect (.exe / .dll):",
				Location = new Point(10, 22),
				AutoSize = true
			};
			txtInput = new TextBox {
				Location = new Point(10, 40),
				Width = grpSource.Width - 96,
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			var btnInput = new Button {
				Text = "Browse...",
				Location = new Point(grpSource.Width - 80, 39),
				Size = new Size(70, 25),
				Anchor = AnchorStyles.Right | AnchorStyles.Top
			};
			btnInput.Click += OnBrowseInput;
			chkPdb = new CheckBox {
				Text = "Load debug symbols from adjacent PDB for renaming analysis",
				Location = new Point(10, 69),
				AutoSize = true
			};
			grpSource.Controls.AddRange(new Control[] { lblInput, txtInput, btnInput, chkPdb });

			var grpOutput = new GroupBox {
				Text = " Output ",
				Location = new Point(12, 114),
				Size = new Size(ContentWidth1(), 128),
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			var lblOutput = new Label {
				Text = "Output folder (empty, or same as the source folder, means: replace the original after backing it up):",
				Location = new Point(10, 22),
				AutoSize = true
			};
			txtOutput = new TextBox {
				Location = new Point(10, 44),
				Width = grpOutput.Width - 96,
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			var btnOutput = new Button {
				Text = "Browse...",
				Location = new Point(grpOutput.Width - 80, 43),
				Size = new Size(70, 25),
				Anchor = AnchorStyles.Right | AnchorStyles.Top
			};
			btnOutput.Click += OnBrowseOutput;
			chkBackup = new CheckBox {
				Text = "When replacing the original file, save a backup of it first (Name_orig.ext)",
				Location = new Point(10, 72),
				AutoSize = true
			};
			chkBackup.Checked = true;
			var lblHint = new Label {
				Text = "Otherwise the protected module is written into the selected output folder.",
				Location = new Point(10, 98),
				AutoSize = true,
				ForeColor = SystemColors.GrayText
			};
			grpOutput.Controls.AddRange(new Control[] { lblOutput, txtOutput, btnOutput, chkBackup, lblHint });

			var grpProtections = new GroupBox {
				Text = " Protections ",
				Location = new Point(12, 248),
				Size = new Size(ContentWidth1(), 246),
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			int half = grpProtections.Width / 2;

			var colLeft = new TableLayoutPanel {
				Location = new Point(8, 22),
				Size = new Size(half - 16, 216),
				ColumnCount = 2,
				RowCount = 5
			};
			colLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
			colLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			for (int i = 0; i < 5; i++)
				colLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));

			var colRight = new TableLayoutPanel {
				Location = new Point(half + 8, 22),
				Size = new Size(half - 16, 216),
				ColumnCount = 2,
				RowCount = 6
			};
			colRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
			colRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			for (int i = 0; i < 6; i++)
				colRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

			chkRename = new CheckBox { Text = "rename", AutoSize = true, Checked = true };
			cboRenameMode = MakeCombo(110, new object[] { "unicode", "ascii", "letters", "decodable", "sequential", "reversible", "empty" });
			chkRename.CheckedChanged += (s, e) => { cboRenameMode.Enabled = chkRename.Checked; };

			chkCtrlFlow = new CheckBox { Text = "ctrl flow", AutoSize = true, Checked = true };
			nudIntensity = new NumericUpDown {
				Minimum = 0,
				Maximum = 100,
				Value = 60,
				Width = 60
			};
			chkCtrlFlow.CheckedChanged += (s, e) => { nudIntensity.Enabled = chkCtrlFlow.Checked; };

			chkConstants = new CheckBox { Text = "constants", AutoSize = true, Checked = true };
			chkResources = new CheckBox { Text = "resources", AutoSize = true };
			chkRefProxy = new CheckBox { Text = "ref proxy", AutoSize = true, Checked = true };

			chkAntiDebug = new CheckBox { Text = "anti debug", AutoSize = true };
			cboAntiDebugMode = MakeCombo(100, new object[] { "win32", "safe", "antinet" });
			cboAntiDebugMode.Enabled = false;
			chkAntiDebug.CheckedChanged += (s, e) => { cboAntiDebugMode.Enabled = chkAntiDebug.Checked; };

			chkAntiIldasm = new CheckBox { Text = "anti ildasm", AutoSize = true, Checked = true };
			chkAntiTamper = new CheckBox { Text = "anti tamper", AutoSize = true };
			cboAntiTamperMode = MakeCombo(100, new object[] { "anti", "jit", "normal" });
			chkAntiTamper.CheckedChanged += (s, e) => { cboAntiTamperMode.Enabled = chkAntiTamper.Checked; };

			chkIntegrity = new CheckBox { Text = "integrity", AutoSize = true };
			chkAntiDump = new CheckBox { Text = "anti dump", AutoSize = true };
			chkTypeScramble = new CheckBox { Text = "typescramble (exp.)", AutoSize = true };

			colLeft.Controls.Add(chkRename, 0, 0);
			colLeft.Controls.Add(cboRenameMode, 1, 0);
			colLeft.Controls.Add(chkCtrlFlow, 0, 1);
			colLeft.Controls.Add(MakeParam(nudIntensity, "intensity"), 1, 1);
			colLeft.Controls.Add(chkConstants, 0, 2);
			colLeft.Controls.Add(chkResources, 0, 3);
			colLeft.Controls.Add(chkRefProxy, 0, 4);

			colRight.Controls.Add(chkAntiDebug, 0, 0);
			colRight.Controls.Add(cboAntiDebugMode, 1, 0);
			colRight.Controls.Add(chkAntiIldasm, 0, 1);
			colRight.Controls.Add(chkAntiTamper, 0, 2);
			colRight.Controls.Add(cboAntiTamperMode, 1, 2);
			colRight.Controls.Add(chkIntegrity, 0, 3);
			colRight.Controls.Add(chkAntiDump, 0, 4);
			colRight.Controls.Add(chkTypeScramble, 0, 5);

			grpProtections.Controls.Add(colLeft);
			grpProtections.Controls.Add(colRight);

			var grpAdvanced = new GroupBox {
				Text = " Advanced ",
				Location = new Point(12, 500),
				Size = new Size(ContentWidth1(), 52),
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
			};
			var lblSeed = new Label {
				Text = "Seed (optional, makes the output reproducible):",
				Location = new Point(10, 22),
				AutoSize = true
			};
			txtSeed = new TextBox {
				Location = new Point(250, 19),
				Width = 220
			};
			grpAdvanced.Controls.Add(lblSeed);
			grpAdvanced.Controls.Add(txtSeed);

			var grpLog = new GroupBox {
				Text = " Log ",
				Location = new Point(12, 558),
				Size = new Size(ContentWidth1(), 164),
				Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
			};
			txtLog = new TextBox {
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				Dock = DockStyle.Fill,
				Font = new Font("Consolas", 8.5F),
				WordWrap = false
			};
			grpLog.Controls.Add(txtLog);

			btnRun = new Button {
				Text = "Obfuscate!",
				Size = new Size(120, 28),
				Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
				Location = new Point(ClientSize.Width - 140, ClientSize.Height - 40)
			};
			btnRun.Click += OnRun;

			btnStop = new Button {
				Text = "Stop",
				Size = new Size(80, 28),
				Enabled = false,
				Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
				Location = new Point(ClientSize.Width - 266, ClientSize.Height - 40)
			};
			btnStop.Click += OnStop;

			btnOpenOutput = new Button {
				Text = "Open output",
				Size = new Size(100, 28),
				Enabled = false,
				Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
				Location = new Point(ClientSize.Width - 372, ClientSize.Height - 40)
			};
			btnOpenOutput.Click += OnOpenOutput;

			lblStatus = new Label {
				Text = "Ready.",
				AutoSize = true,
				Location = new Point(12, ClientSize.Height - 34),
				Anchor = AnchorStyles.Left | AnchorStyles.Bottom
			};

			Controls.AddRange(new Control[] {
				grpSource, grpOutput, grpProtections, grpAdvanced, grpLog,
				btnOpenOutput, btnStop, btnRun, lblStatus
			});
		}

		int ContentWidth1() {
			return ClientSize.Width - 24;
		}

		static ComboBox MakeCombo(int width, object[] items) {
			var cbo = new ComboBox {
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = width
			};
			cbo.Items.AddRange(items);
			cbo.SelectedIndex = 0;
			return cbo;
		}

		static Panel MakeParam(Control ctrl, string label) {
			var panel = new Panel {
				Height = 30,
				Margin = new Padding(0)
			};
			var lbl = new Label {
				Text = label,
				AutoSize = true,
				Location = new Point(3, 6)
			};
			ctrl.Location = new Point(70, 4);
			panel.Controls.Add(lbl);
			panel.Controls.Add(ctrl);
			return panel;
		}

		QuickOptions CollectOptions() {
			return new QuickOptions {
				Rename = chkRename.Checked,
				RenameMode = chkRename.Checked ? (string)cboRenameMode.SelectedItem : null,
				CtrlFlow = chkCtrlFlow.Checked,
				Intensity = (int)nudIntensity.Value,
				Constants = chkConstants.Checked,
				Resources = chkResources.Checked,
				RefProxy = chkRefProxy.Checked,
				AntiDebug = chkAntiDebug.Checked,
				AntiDebugMode = chkAntiDebug.Checked ? (string)cboAntiDebugMode.SelectedItem : null,
				AntiIldasm = chkAntiIldasm.Checked,
				AntiTamper = chkAntiTamper.Checked,
				AntiTamperMode = chkAntiTamper.Checked ? (string)cboAntiTamperMode.SelectedItem : null,
				Integrity = chkIntegrity.Checked,
				AntiDump = chkAntiDump.Checked,
				TypeScramble = chkTypeScramble.Checked,
				LoadPdb = chkPdb.Checked,
				BackupOriginal = chkBackup.Checked,
				Seed = txtSeed.Text
			};
		}

		void OnBrowseInput(object sender, EventArgs e) {
			using (var dialog = new OpenFileDialog()) {
				dialog.Filter = ".NET assemblies (*.exe;*.dll;*.netmodule;*.winmd)|*.exe;*.dll;*.netmodule;*.winmd|All files (*.*)|*.*";
				dialog.Title = "Select the module to protect";
				if (File.Exists(txtInput.Text)) {
					try {
						dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(txtInput.Text));
					}
					catch (ArgumentException) {
					}
				}
				if (dialog.ShowDialog(this) != DialogResult.OK)
					return;
				txtInput.Text = dialog.FileName;
				if (string.IsNullOrEmpty(txtOutput.Text))
					txtOutput.Text = Path.GetDirectoryName(dialog.FileName);
				chkPdb.Checked = File.Exists(Path.ChangeExtension(dialog.FileName, ".pdb"));
			}
		}

		void OnBrowseOutput(object sender, EventArgs e) {
			using (var dialog = new FolderBrowserDialog()) {
				if (Directory.Exists(txtOutput.Text)) {
					try {
						dialog.SelectedPath = Path.GetFullPath(txtOutput.Text);
					}
					catch (ArgumentException) {
					}
				}
				else if (File.Exists(txtInput.Text)) {
					dialog.SelectedPath = Path.GetDirectoryName(txtInput.Text);
				}
				dialog.Description = "Select the output folder for the protected module";
				if (dialog.ShowDialog(this) == DialogResult.OK)
					txtOutput.Text = dialog.SelectedPath;
			}
		}

		void OnRun(object sender, EventArgs e) {
			string input = txtInput.Text.Trim();
			if (input.Length == 0 || !File.Exists(input)) {
				MessageBox.Show(this, "Please select an existing .exe or .dll to protect.", "Input required",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string output = txtOutput.Text.Trim();
			if (output.Length == 0)
				output = Path.GetDirectoryName(Path.GetFullPath(input));

			var options = CollectOptions();
			logger = new QuickLogger();
			logger.OnOutput = msg => {
				if (IsHandleCreated) {
					try {
						BeginInvoke((Action)(() => AppendLog(msg)));
					}
					catch (ObjectDisposedException) {
					}
				}
			};

			cts = new CancellationTokenSource();
			btnRun.Enabled = false;
			btnStop.Enabled = true;
			btnOpenOutput.Enabled = false;
			lastResult = null;
			lblStatus.Text = "Running...";

			var capturedInput = input;
			var capturedOutput = output;
			var capturedLogger = logger;
			var capturedToken = cts.Token;

			QuickRunner.RunAsync(capturedInput, capturedOutput, options, capturedLogger, capturedToken)
				.ContinueWith(t => OnRunFinished(t), CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.FromCurrentSynchronizationContext());
		}

		void AppendLog(string msg) {
			txtLog.AppendText(msg + Environment.NewLine);
		}

		void OnStop(object sender, EventArgs e) {
			if (cts == null)
				return;
			try {
				cts.Cancel();
			}
			catch (ObjectDisposedException) {
			}
			lblStatus.Text = "Stopping...";
		}

		void OnRunFinished(Task<RunResult> task) {
			btnRun.Enabled = true;
			btnStop.Enabled = false;
			cts = null;

			if (task.IsFaulted) {
				var ex = task.Exception.GetBaseException();
				AppendLog("Exception: " + ex);
				lblStatus.Text = "Failed: " + ex.Message;
				MessageBox.Show(this, "Unexpected error: " + ex.Message, "Obfuscation failed",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			lastResult = task.Result;
			if (lastResult == null || string.IsNullOrEmpty(lastResult.OutputPath)) {
				lblStatus.Text = "Failed.";
				MessageBox.Show(this, "Obfuscation did not complete successfully; see the log for details.", "Failed",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (!string.IsNullOrEmpty(lastResult.BackupPath))
				lblStatus.Text = "Done (backup: " + Path.GetFileName(lastResult.BackupPath) + ")";
			else
				lblStatus.Text = "Done.";
			btnOpenOutput.Enabled = true;
		}

		void OnOpenOutput(object sender, EventArgs e) {
			if (lastResult == null || string.IsNullOrEmpty(lastResult.OutputPath))
				return;
			try {
				string target = lastResult.OutputPath;
				if (File.Exists(target))
					System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + target + "\"");
				else
					System.Diagnostics.Process.Start("explorer.exe", "\"" + target + "\"");
			}
			catch (Exception ex) {
				MessageBox.Show(this, "Failed to open the folder: " + ex.Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
