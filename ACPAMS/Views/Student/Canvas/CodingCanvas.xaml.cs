using ACPAMS.Models;
using ACPAMS.Services;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

namespace ACPAMS.Views.Student.Canvas
{
    public partial class CodingCanvas : UserControl
    {
        private string _language = "C";
        private readonly CodeRunnerService _runner = new();
        private string? _lastExecutionOutput;

        // Custom VS Code Dark+ themed .xshd files, loaded once and cached.
        // These replace AvalonEdit's built-in highlighters, which use a
        // muted default palette that's hard to read against our dark
        // editor background.
        private static readonly Dictionary<string, IHighlightingDefinition> CustomHighlighting = new();

        // Default starter stub + custom highlighting key + file name per language.
        private static readonly Dictionary<string, (string FileName, string Stub, string SyntaxKey)> LanguageDefaults = new()
        {
            ["C"] = ("main.c",
                "#include <stdio.h>\n\nint main() {\n    // Write your solution here\n\n    return 0;\n}",
                "C"),
            ["Python"] = ("main.py",
                "def main():\n    # Write your solution here\n    pass\n\nif __name__ == \"__main__\":\n    main()",
                "Python"),
            ["Java"] = ("Main.java",
                "public class Main {\n    public static void main(String[] args) {\n        // Write your solution here\n\n    }\n}",
                "Java"),
        };

        public CodingCanvas()
        {
            InitializeComponent();
        }

        // Called by StudentExamPage when loading a question
        public CodingCanvas(Question question, StudentAnswer? existingAnswer = null)
        {
            InitializeComponent();

            QuestionText.Text = question.Text;

            // Determine language: existing answer > question default > C
            if (!string.IsNullOrEmpty(existingAnswer?.LanguageUsed) && LanguageDefaults.ContainsKey(existingAnswer.LanguageUsed))
                _language = existingAnswer.LanguageUsed;
            else if (!string.IsNullOrEmpty(question.ProgrammingLanguage) && LanguageDefaults.ContainsKey(question.ProgrammingLanguage))
                _language = question.ProgrammingLanguage;
            else
                _language = "C";

            SelectLanguageInComboBox(_language);
            ApplyLanguageSettings(_language);

            if (!string.IsNullOrEmpty(existingAnswer?.SourceCode))
            {
                CodeEditor.Text = existingAnswer.SourceCode;
            }
            else if (!string.IsNullOrEmpty(question.StarterCode))
            {
                CodeEditor.Text = question.StarterCode;
            }
            else if (LanguageDefaults.TryGetValue(_language, out var def))
            {
                CodeEditor.Text = def.Stub;
            }

            if (!string.IsNullOrEmpty(existingAnswer?.ExecutionOutput))
            {
                _lastExecutionOutput = existingAnswer.ExecutionOutput;
                OutputResultText.Text = existingAnswer.ExecutionOutput;
                OutputResultText.Foreground = System.Windows.Media.Brushes.WhiteSmoke;
            }
        }

        // Loads (and caches) a custom .xshd highlighting definition by
        // language key. Files are embedded resources under
        // Resources/Syntax/{key}-Dark.xshd — make sure each .xshd's
        // Build Action is set to "Embedded Resource" in its file
        // properties in Visual Studio.
        private static IHighlightingDefinition LoadCustomHighlighting(string key)
        {
            if (CustomHighlighting.TryGetValue(key, out var cached))
                return cached;

            string resourceName = $"ACPAMS.Resources.Syntax.{key}-Dark.xshd";
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException(
                    $"Embedded syntax file not found: {resourceName}. " +
                    "Ensure the .xshd file's Build Action is set to 'Embedded Resource'.");

            using var reader = new XmlTextReader(stream);
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            CustomHighlighting[key] = definition;
            return definition;
        }

        // Selects the matching ComboBoxItem by Tag without firing the
        // SelectionChanged starter-code-replacement logic
        private void SelectLanguageInComboBox(string language)
        {
            foreach (var item in LanguageSelector.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == language)
                {
                    LanguageSelector.SelectionChanged -= LanguageSelector_SelectionChanged;
                    LanguageSelector.SelectedItem = cbi;
                    LanguageSelector.SelectionChanged += LanguageSelector_SelectionChanged;
                    return;
                }
            }
        }

        // Updates the file tab name and switches AvalonEdit's syntax highlighter
        private void ApplyLanguageSettings(string language)
        {
            if (!LanguageDefaults.TryGetValue(language, out var def)) return;

            FileNameText.Text = def.FileName;
            CodeEditor.SyntaxHighlighting = LoadCustomHighlighting(def.SyntaxKey);
        }

        // ── Language switching ──────────────────────────────────────────
        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is not ComboBoxItem item) return;

            string newLanguage = item.Tag?.ToString() ?? "C";
            if (newLanguage == _language) return;

            // Warn before wiping code, since switching language resets the stub
            if (!string.IsNullOrWhiteSpace(CodeEditor.Text))
            {
                var result = MessageBox.Show(
                    $"Switching to {newLanguage} will replace your current code with a starter template. Continue?",
                    "Switch Language", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    SelectLanguageInComboBox(_language);
                    return;
                }
            }

            _language = newLanguage;
            ApplyLanguageSettings(_language);

            if (LanguageDefaults.TryGetValue(_language, out var def))
                CodeEditor.Text = def.Stub;

            // Clear stale output from the previous language run
            OutputResultText.Text = string.Empty;
        }

        // ── View Question toggle ────────────────────────────────────────
        private void ViewQuestionBtn_Click(object sender, RoutedEventArgs e)
        {
            QuestionStrip.Visibility = QuestionStrip.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void CloseQuestionBtn_Click(object sender, RoutedEventArgs e)
        {
            QuestionStrip.Visibility = Visibility.Collapsed;
        }

        // ── Run Code — actually compiles/executes via CodeRunnerService ─
        private async void RunCodeBtn_Click(object sender, RoutedEventArgs e)
        {
            var runButton = (Button)sender;
            runButton.IsEnabled = false;

            OutputCommandText.Inlines.Clear();
            OutputCommandText.Inlines.Add(new System.Windows.Documents.Run("$ ")
            {
                Foreground = System.Windows.Media.Brushes.LimeGreen
            });
            OutputCommandText.Inlines.Add(new System.Windows.Documents.Run(
                $"Running {FileNameText.Text}...")
            {
                Foreground = System.Windows.Media.Brushes.Gray
            });
            OutputResultText.Text = string.Empty;

            try
            {
                var result = await _runner.RunAsync(_language, CodeEditor.Text);

                OutputResultText.Foreground = result.Success
                    ? System.Windows.Media.Brushes.WhiteSmoke
                    : System.Windows.Media.Brushes.IndianRed;

                OutputResultText.Text = result.Success
                    ? (string.IsNullOrWhiteSpace(result.Output)
                        ? "(program ran with no output)"
                        : result.Output)
                    : (!string.IsNullOrWhiteSpace(result.Error) ? result.Error : result.Output);

                _lastExecutionOutput = !string.IsNullOrWhiteSpace(result.Output) && !string.IsNullOrWhiteSpace(result.Error)
                    ? $"{result.Output}\n{result.Error}"
                    : (!string.IsNullOrWhiteSpace(result.Output) ? result.Output : result.Error);
            }
            catch (Exception ex)
            {
                OutputResultText.Foreground = System.Windows.Media.Brushes.IndianRed;
                OutputResultText.Text = $"Unexpected error: {ex.Message}";
                _lastExecutionOutput = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                runButton.IsEnabled = true;
            }
        }

        // ── Ln/Col indicator — AvalonEdit fires TextChanged on edits ────
        private void CodeEditor_TextChanged(object sender, EventArgs e)
        {
            var location = CodeEditor.Document.GetLocation(CodeEditor.CaretOffset);
            LineColText.Text = $"Ln {location.Line}, Col {location.Column}";
        }

        // ── Called by StudentExamPage to extract code before navigating ─
        public string GetSourceCode() => CodeEditor?.Text ?? string.Empty;
        public string GetLanguage() => _language;
        public string? GetExecutionOutput() => _lastExecutionOutput;
    }
}