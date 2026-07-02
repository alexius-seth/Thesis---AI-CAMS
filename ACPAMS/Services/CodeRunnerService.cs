using System.Diagnostics;
using System.IO;
using System.Text;

namespace ACPAMS.Services
{
    /// <summary>
    /// Compiles and runs student code locally by shelling out to the
    /// language toolchain installed on the lab PC:
    ///   - Java   → requires JDK (javac + java) on PATH
    ///   - Python → requires Python 3 (python or python3) on PATH
    ///   - C      → requires a C compiler (gcc via MinGW, or cl.exe) on PATH
    ///
    /// All work happens in a temp folder that is deleted after each run.
    /// This is NOT sandboxed/secure — it runs with the same privileges
    /// as the exam app. Acceptable for a controlled lab environment
    /// (LR4) where the app already runs in kiosk mode, but should NOT
    /// be exposed to untrusted code in any other context.
    /// </summary>
    public class CodeRunnerService
    {
        private const int TimeoutMs = 5000; // kill runaway code after 5s

        public async Task<RunResult> RunAsync(string language, string sourceCode)
        {
            return language switch
            {
                "Java" => await RunJavaAsync(sourceCode),
                "Python" => await RunPythonAsync(sourceCode),
                "C" => await RunCAsync(sourceCode),
                _ => new RunResult(false, string.Empty,
                                $"Unsupported language: {language}")
            };
        }

        // ── JAVA ────────────────────────────────────────────────────────
        private async Task<RunResult> RunJavaAsync(string sourceCode)
        {
            string workDir = CreateTempDir();
            try
            {
                // Java requires the public class name to match the file name
                string className = ExtractJavaClassName(sourceCode) ?? "Main";
                string filePath = Path.Combine(workDir, $"{className}.java");
                await File.WriteAllTextAsync(filePath, sourceCode);

                var compile = await RunProcessAsync("javac", $"\"{filePath}\"", workDir);
                if (!compile.Success)
                    return new RunResult(false, string.Empty, compile.Error);

                var run = await RunProcessAsync("java", $"-cp \"{workDir}\" {className}", workDir);
                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        private static string? ExtractJavaClassName(string sourceCode)
        {
            // Looks for: public class <Name>
            var match = System.Text.RegularExpressions.Regex.Match(
                sourceCode, @"public\s+class\s+(\w+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        // ── PYTHON ──────────────────────────────────────────────────────
        private async Task<RunResult> RunPythonAsync(string sourceCode)
        {
            string workDir = CreateTempDir();
            string filePath = Path.Combine(workDir, "main.py");
            try
            {
                await File.WriteAllTextAsync(filePath, sourceCode);

                // Try "python" first (common on Windows), fall back to "python3"
                var run = await RunProcessAsync("python", $"\"{filePath}\"", workDir);
                if (!run.Success && run.Error.Contains("not recognized", StringComparison.OrdinalIgnoreCase))
                    run = await RunProcessAsync("python3", $"\"{filePath}\"", workDir);

                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        // ── C ───────────────────────────────────────────────────────────
        private async Task<RunResult> RunCAsync(string sourceCode)
        {
            string workDir = CreateTempDir();
            string filePath = Path.Combine(workDir, "main.c");
            string exePath = Path.Combine(workDir,
                OperatingSystem.IsWindows() ? "main.exe" : "main");
            try
            {
                await File.WriteAllTextAsync(filePath, sourceCode);

                var compile = await RunProcessAsync("gcc",
                    $"\"{filePath}\" -o \"{exePath}\"", workDir);
                if (!compile.Success)
                    return new RunResult(false, string.Empty, compile.Error);

                var run = await RunProcessAsync($"\"{exePath}\"", string.Empty, workDir);
                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        // ── Process execution helper with timeout ─────────────────────
        private async Task<(bool Success, string Output, string Error)> RunProcessAsync(
            string fileName, string arguments, string workingDirectory)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = await Task.Run(() => process.WaitForExit(TimeoutMs));

                if (!exited)
                {
                    try { process.Kill(true); } catch { }
                    return (false, stdout.ToString(),
                        "Execution timed out (exceeded 5 seconds). " +
                        "Check for infinite loops.");
                }

                bool success = process.ExitCode == 0;
                return (success, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Thrown when the executable (gcc, javac, python) isn't found on PATH
                return (false, string.Empty,
                    $"'{fileName}' is not installed or not on PATH on this machine. " +
                    "Ask your lab administrator to install the required toolchain.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Execution error: {ex.Message}");
            }
        }

        // ── Temp folder management ─────────────────────────────────────
        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "acpams_run_" + Guid.NewGuid());
            Directory.CreateDirectory(path);
            return path;
        }

        private static void CleanupTempDir(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { }
        }
    }

    public record RunResult(bool Success, string Output, string Error);
}