using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Confuser.QuickTool {
	internal static class Program {
		[STAThread]
		static int Main(string[] args) {
			if (args.Length >= 3 && string.Equals(args[0], "headless", StringComparison.OrdinalIgnoreCase))
				return RunHeadless(args);

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
			return 0;
		}

		static int RunHeadless(string[] args) {
			string input = args[1];
			string output = args[2];
			var options = QuickRunner.DefaultOptions();
			var logger = new QuickLogger();
			logger.OnOutput = msg => Console.WriteLine(msg);
			if (!File.Exists(input)) {
				Console.WriteLine("Input file not found: " + input);
				return 1;
			}
			var task = QuickRunner.RunAsync(input, output, options, logger, CancellationToken.None);
			task.Wait();
			if (logger.Success) {
				Console.WriteLine("Output: " + task.Result.OutputPath);
				if (!string.IsNullOrEmpty(task.Result.BackupPath))
					Console.WriteLine("Backup: " + task.Result.BackupPath);
				return 0;
			}
			return 1;
		}
	}
}
