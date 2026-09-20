using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    /// <summary>
    /// Initial setup modal shown before CreateExamPage loads.
    /// Lets the instructor pick: Examination | Quiz | Activity.
    ///
    /// Usage:
    ///     var modal = new TaskCategoryModal { Owner = this };
    ///     if (modal.ShowDialog() == true)
    ///     {
    ///         string category = modal.SelectedCategory; // "Examination", "Quiz", "Activity"
    ///         // open CreateExamPage with the selected category
    ///     }
    /// </summary>
    public partial class TaskCategoryModal : Window
    {
        public string SelectedCategory { get; private set; } = "Examination";

        private Button? _selectedBtn;

        // Maps each category button to its check indicator border
        private readonly Dictionary<Button, Border> _checkMap = new();

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMNCRP_DISABLED = 1;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public TaskCategoryModal()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int policy = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
            SyncToOwner();
        }

        private void SyncToOwner()
        {
            var owner = Owner ?? Application.Current.MainWindow;
            if (owner != null)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                if (owner.WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    Left = owner.Left;
                    Top = owner.Top;
                    Width = owner.ActualWidth;
                    Height = owner.ActualHeight;
                }
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Left = 0;
                Top = 0;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncToOwner();

            // Build the check indicator map after InitializeComponent
            // so the named elements are available
            _checkMap[BtnExamination] = FindCheckBorder(BtnExamination);
            _checkMap[BtnQuiz]        = FindCheckBorder(BtnQuiz);
            _checkMap[BtnActivity]    = FindCheckBorder(BtnActivity);

            // Default selection: Examination
            _selectedBtn = BtnExamination;
        }

        // ── Category selection ───────────────────────────────────────────
        private void CategoryOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            // Deselect previous
            if (_selectedBtn != null && _selectedBtn != clicked)
            {
                _selectedBtn.Style = (Style)FindResource("Btn.CategoryOption");
                if (_checkMap.TryGetValue(_selectedBtn, out var oldCheck))
                    SetCheckSelected(oldCheck, false);
            }

            // Select new
            clicked.Style = (Style)FindResource("Btn.CategoryOption.Selected");
            if (_checkMap.TryGetValue(clicked, out var newCheck))
                SetCheckSelected(newCheck, true);

            _selectedBtn     = clicked;
            SelectedCategory = clicked.Tag?.ToString() ?? "Examination";
        }

        // Fills/empties the radio-circle indicator at the right of each card
        private void SetCheckSelected(Border check, bool selected)
        {
            if (selected)
            {
                check.Background = (System.Windows.Media.SolidColorBrush)
                    FindResource("Accent.Primary");
                check.BorderThickness = new Thickness(0);

                // Show checkmark inside
                if (check.Child is TextBlock tb)
                    tb.Visibility = Visibility.Visible;
            }
            else
            {
                check.Background = System.Windows.Media.Brushes.Transparent;
                check.BorderThickness = new Thickness(1.5);
                check.BorderBrush = (System.Windows.Media.SolidColorBrush)
                    FindResource("Border.Medium");

                if (check.Child is TextBlock tb)
                    tb.Visibility = Visibility.Collapsed;
            }
        }

        // Walks the visual tree of a card button to find its check Border
        // (the rightmost column element in the card Grid)
        private static Border FindCheckBorder(Button btn)
        {
            if (btn.Content is Grid grid &&
                grid.Children.Count >= 3 &&
                grid.Children[2] is Border border)
                return border;

            // Fallback — return a dummy border so null checks aren't needed
            return new Border();
        }

        // ── Footer buttons ───────────────────────────────────────────────
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
