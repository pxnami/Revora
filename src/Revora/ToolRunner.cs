using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Revora
{
    public sealed class ToolResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public void EnsureSuccess(string action)
        {
            if (ExitCode != 0)
                throw new InvalidOperationException(action + " failed (exit " + ExitCode + ").\n" + Output);
        }
    }

    public interface IToolRunner
    {
        Task<ToolResult> RunAsync(string tool, IEnumerable<string> arguments, int timeoutSeconds, Action<string> output);
    }

    public sealed class ToolRunner : IToolRunner
    {
        public static readonly string[] RequiredTools = { "idevice_id", "ideviceinfo", "ideviceenterrecovery", "irecovery", "idevicerestore", "plistutil" };
        public string DirectoryPath { get; private set; }
        private readonly string workingDirectory;

        public ToolRunner(string directoryPath, string workingDirectory)
        {
            DirectoryPath = Path.GetFullPath(directoryPath);
            this.workingDirectory = Path.GetFullPath(workingDirectory);
        }

        public string[] MissingTools()
        {
            return RequiredTools.Where(tool => !File.Exists(Path.Combine(DirectoryPath, tool + ".exe"))).ToArray();
        }

        public async Task<ToolResult> RunAsync(string tool, IEnumerable<string> arguments, int timeoutSeconds, Action<string> output)
        {
            if (!RequiredTools.Contains(tool)) throw new ArgumentException("Unknown device tool.", "tool");
            string executable = Path.Combine(DirectoryPath, tool + ".exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Missing " + tool + ".exe. Open Setup to configure the device tools.", executable);
            Directory.CreateDirectory(workingDirectory);
            var start = new ProcessStartInfo(executable) {
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                WorkingDirectory = workingDirectory, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, RedirectStandardInput = true
            };
            var collected = new StringBuilder();
            var gate = new object();
            using (var process = new Process { StartInfo = start })
            {
                var stdoutDone = new TaskCompletionSource<bool>();
                var stderrDone = new TaskCompletionSource<bool>();
                Action<string> accept = line => {
                    lock (gate) {
                        collected.AppendLine(line);
                        if (collected.Length > 65536) collected.Remove(0, collected.Length - 65536);
                    }
                    if (output != null) output(line);
                };
                process.OutputDataReceived += (sender, e) => { if (e.Data == null) stdoutDone.TrySetResult(true); else accept(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data == null) stderrDone.TrySetResult(true); else accept(e.Data); };
                try { process.Start(); }
                catch (Win32Exception e) {
                    throw new Win32Exception(e.NativeErrorCode,
                        "Windows could not start " + executable + " (error " + e.NativeErrorCode + ").\n" + e.Message);
                }
                process.StandardInput.Close();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds <= 0 ? -1 : checked(timeoutSeconds * 1000)));
                if (!exited) {
                    process.Kill();
                    await Task.Run(() => process.WaitForExit());
                }
                await Task.WhenAll(stdoutDone.Task, stderrDone.Task);
                if (!exited) throw new TimeoutException(tool + " did not respond within " + timeoutSeconds + " seconds. Check the USB connection and Apple device drivers.");
                return new ToolResult { ExitCode = process.ExitCode, Output = collected.ToString() };
            }
        }

        public static string QuoteArgument(string argument)
        {
            if (argument == null) throw new ArgumentNullException("argument");
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2).Append('"');
            return result.ToString();
        }
    }
}
