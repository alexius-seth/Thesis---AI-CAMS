using System.Diagnostics;
using System.IO;
using System.Text;

namespace ACPAMS.Services
{
    /// <summary>
    /// Compiles and runs student code locally using portable toolchains under Tools/
    /// (or fallback system PATH if portable tools are not present):
    ///   - C      → Tools/MinGW/bin/gcc.exe (fallback: gcc)
    ///   - Python → Tools/Python/python.exe (fallback: python / python3)
    ///   - Java   → Tools/JDK/bin/javac.exe and java.exe (fallback: javac / java)
    /// </summary>
    public class CodeRunnerService
    {
        private const int TimeoutMs = 5000; // hard process execution timeout: 5s

        public async Task<RunResult> RunAsync(string language, string sourceCode, string? stdinInput = null)
        {
            return language switch
            {
                "Java" => await RunJavaAsync(sourceCode, stdinInput),
                "Python" => await RunPythonAsync(sourceCode, stdinInput),
                "C" => await RunCAsync(sourceCode, stdinInput),
                _ => new RunResult(false, string.Empty, $"Unsupported language: {language}")
            };
        }

        // ── TOOLCHAIN RESOLUTION ─────────────────────────────────────────
        private static (string Path, string? Error) ResolveTool(string relativeToolPath, string[] fallbackBinaries, string toolDisplayName)
        {
            string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", relativeToolPath);
            if (File.Exists(portable))
                return (portable, null);

            foreach (var binary in fallbackBinaries)
            {
                if (IsBinaryOnPath(binary))
                    return (binary, null);
            }

            return (string.Empty,
                $"{toolDisplayName} not found at '{portable}'. " +
                "Please ensure the portable toolchain is placed in the Tools/ directory or installed on system PATH.");
        }

        private static bool IsBinaryOnPath(string binaryName)
        {
            try
            {
                var envPath = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(envPath)) return false;

                foreach (var dir in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    var full = Path.Combine(dir.Trim(), binaryName);
                    if (File.Exists(full) || (OperatingSystem.IsWindows() && File.Exists(full + ".exe")))
                        return true;
                }
            }
            catch { }
            return false;
        }

        // ── JAVA ────────────────────────────────────────────────────────
        private async Task<RunResult> RunJavaAsync(string sourceCode, string? stdinInput)
        {
            var (javacPath, javacErr) = ResolveTool(Path.Combine("JDK", "bin", "javac.exe"), ["javac"], "Java compiler (javac)");
            if (javacErr != null)
                return new RunResult(false, string.Empty, javacErr);

            var (javaPath, javaErr) = ResolveTool(Path.Combine("JDK", "bin", "java.exe"), ["java"], "Java runtime (java)");
            if (javaErr != null)
                return new RunResult(false, string.Empty, javaErr);

            string workDir = CreateTempDir();
            try
            {
                string className = ExtractJavaClassName(sourceCode) ?? "Main";
                string filePath = Path.Combine(workDir, $"{className}.java");
                await File.WriteAllTextAsync(filePath, sourceCode);

                var compile = await RunProcessAsync(javacPath, $"\"{filePath}\"", workDir);
                if (!compile.Success)
                    return new RunResult(false, string.Empty, compile.Error);

                var run = await RunProcessAsync(javaPath, $"-cp \"{workDir}\" {className}", workDir, stdinInput);
                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        private static string? ExtractJavaClassName(string sourceCode)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                sourceCode, @"public\s+class\s+(\w+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        // ── PYTHON ──────────────────────────────────────────────────────
        private async Task<RunResult> RunPythonAsync(string sourceCode, string? stdinInput)
        {
            var (pythonPath, pythonErr) = ResolveTool(Path.Combine("Python", "python.exe"), ["python", "python3"], "Python interpreter (python)");
            if (pythonErr != null)
                return new RunResult(false, string.Empty, pythonErr);

            string workDir = CreateTempDir();
            string filePath = Path.Combine(workDir, "main.py");
            try
            {
                await File.WriteAllTextAsync(filePath, sourceCode);

                var run = await RunProcessAsync(pythonPath, $"\"{filePath}\"", workDir, stdinInput);
                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        // ── C ───────────────────────────────────────────────────────────
        private async Task<RunResult> RunCAsync(string sourceCode, string? stdinInput)
        {
            var (gccPath, gccErr) = ResolveTool(Path.Combine("MinGW", "bin", "gcc.exe"), ["gcc"], "C compiler (gcc)");
            if (gccErr != null)
                return new RunResult(false, string.Empty, gccErr);

            string workDir = CreateTempDir();
            string filePath = Path.Combine(workDir, "main.c");
            string exePath = Path.Combine(workDir, OperatingSystem.IsWindows() ? "main.exe" : "main");
            try
            {
                await File.WriteAllTextAsync(filePath, sourceCode);

                var compile = await RunProcessAsync(gccPath, $"\"{filePath}\" -o \"{exePath}\"", workDir);
                if (!compile.Success)
                    return new RunResult(false, string.Empty, compile.Error);

                var run = await RunProcessAsync($"\"{exePath}\"", string.Empty, workDir, stdinInput);
                return new RunResult(run.Success, run.Output, run.Error);
            }
            finally
            {
                CleanupTempDir(workDir);
            }
        }

        // ── Process execution helper with timeout and stdin streaming ──
        private async Task<(bool Success, string Output, string Error)> RunProcessAsync(
            string fileName, string arguments, string workingDirectory, string? stdinInput = null)
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
                        RedirectStandardInput = true,
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

                if (!string.IsNullOrEmpty(stdinInput))
                {
                    await process.StandardInput.WriteAsync(stdinInput);
                    await process.StandardInput.FlushAsync();
                }
                process.StandardInput.Close();

                bool exited = await Task.Run(() => process.WaitForExit(TimeoutMs));

                if (!exited)
                {
                    try { process.Kill(true); } catch { }
                    return (false, stdout.ToString().TrimEnd(),
                        "Execution timed out (exceeded 5 seconds). Check for infinite loops or unread input.");
                }

                bool success = process.ExitCode == 0;
                return (success, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return (false, string.Empty,
                    $"'{fileName}' could not be started. Verify the toolchain executable is accessible.");
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
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                try
                {
                    var extensions = new[] { ".c", ".py", ".java", ".class", ".exe" };
                    foreach (var file in Directory.GetFiles(path))
                    {
                        if (extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    Directory.Delete(path, recursive: true);
                }
                catch { }
            }
        }
    }

    public record RunResult(bool Success, string Output, string Error);
}
