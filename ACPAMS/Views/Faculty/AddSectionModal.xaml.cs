using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    /// <summary>
    /// Add Section Modal — triggered by the "+ Add Section" button on CreateExamPage.
    ///
    /// Usage:
    ///     var modal = new AddSectionModal { Owner = this };
    ///     if (modal.ShowDialog() == true)
    ///     {
    ///         string name         = modal.SectionName;
    ///         string type         = modal.QuestionType;
    ///         string instructions = modal.Instructions;
    ///         // add to exam section list
    ///     }
    ///
    /// Back-end wiring:
    ///   - Bind SectionName, QuestionType, Instructions to your ExamSection model
    ///   - Save to database via ExamService or directly via AppDbContext
    /// </summary>
    public partial class AddSectionModal : Window
    {
        // ── Output properties — read these after ShowDialog() == true ──
        public string SectionName   { get; private set; } = string.Empty;
        public string QuestionType  { get; private set; } = string.Empty;
        public string Instructions  { get; private set; } = string.Empty;

        // Track selected type button
        private Button? _selectedTypeBtn = null;

        private readonly string[] _typeLabels = new[]
        {
            "identification", "multiple_choice", "true_false",
            "essay", "enumeration", "coding"
        };

        private readonly Dictionary<string, string> _typeDisplayNames = new()
        {
            { "identification",  "Identification"  },
            { "multiple_choice", "Multiple Choice" },
            { "true_false",      "True or False"   },
            { "essay",           "Essay"            },
            { "enumeration",     "Enumeration"     },
            { "coding",          "Coding"          }
        };

        public AddSectionModal()
        {
            InitializeComponent();
        }

        // ── Type toggle buttons ─────────────────────────────────────────
        private void TypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            // Reset previously selected button style
            if (_selectedTypeBtn != null)
                _selectedTypeBtn.Style = (Style)FindResource("Btn.TypeToggle");

            // Apply selected style
            clicked.Style = (Style)FindResource("Btn.TypeToggle.Selected");
            _selectedTypeBtn = clicked;

            // Store the selected type from Tag
            QuestionType = clicked.Tag?.ToString() ?? string.Empty;

            // Update label
            if (_typeDisplayNames.TryGetValue(QuestionType, out var display))
                SelectedTypeLabel.Text = $"Selected: {display}";
        }

        // ── Save ────────────────────────────────────────────────────────
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = SectionNameBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a section name.",
                    "Missing Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                SectionNameBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(QuestionType))
            {
                MessageBox.Show("Please select a question type for this section.",
                    "Missing Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SectionName  = name;
            Instructions = InstructionsBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        // ── Cancel / Close ──────────────────────────────────────────────
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
