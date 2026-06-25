using System.Windows;

namespace ACPAMS.Views.Faculty
{
    /// <summary>
    /// AI Rubric Configuration Modal.
    /// Open via: new AIRubricModal { Owner = Application.Current.MainWindow }.ShowDialog()
    /// 
    /// Back-end wiring needed:
    ///   - Populate header context (question title, section name) via constructor params or ViewModel
    ///   - Scope toggle (Single Question vs Entire Section) → bind to RubricScopeViewModel property
    ///   - AI Prompt TextBox → bind to AIInstructionText property
    ///   - Matrix rows → bind to ObservableCollection&lt;RubricCriteriaRow&gt;
    ///   - Add Row / Add Column buttons → relay commands on ViewModel
    ///   - Delete row (trash icon) → RemoveCriteriaCommand with row index param
    ///   - File drop zone → AllowDrop="True" + DragEnter/Drop events in code-behind
    ///   - Browse files button → OpenFileDialog with PDF/PPTX/DOCX/PNG filter
    ///   - Cancel button → this.DialogResult = false; this.Close();
    ///   - Save &amp; Attach Rubric → validate, save, this.DialogResult = true; this.Close();
    /// </summary>
    public partial class AIRubricModal : Window
    {
        public AIRubricModal()
        {
            InitializeComponent();
        }
    }
}
