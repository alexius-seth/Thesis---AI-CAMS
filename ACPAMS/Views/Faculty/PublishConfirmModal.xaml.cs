using System.Windows;

namespace ACPAMS.Views.Faculty
{
    /// <summary>
    /// Publish Confirmation Modal — shown when faculty clicks "Save and Publish" on CreateExamPage.
    /// Open via: new PublishConfirmModal { Owner = this }.ShowDialog()
    ///
    /// Back-end wiring needed:
    ///   - Course Code TextBox      → bind to CourseCode property; triggers exam code generation on change
    ///   - Time Limit TextBox       → bind to TimeLimitMinutes (int); validate numeric input only
    ///   - Generated Exam Code      → bind to GeneratedExamCode (read-only, auto-populated by service)
    ///   - Copy button              → Clipboard.SetText(GeneratedExamCode)
    ///   - Exam Name TextBox        → bind to ExamName property
    ///   - Close (X) button         → this.DialogResult = false; this.Close()
    ///   - Finish and Publish btn   → validate all fields, call PublishExamCommand, 
    ///                                this.DialogResult = true; this.Close()
    /// </summary>
    public partial class PublishConfirmModal : Window
    {
        public PublishConfirmModal()
        {
            InitializeComponent();
        }
    }
}
