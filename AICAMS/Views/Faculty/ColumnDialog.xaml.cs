using System.Windows;
using System.Windows.Input;

namespace ACPAMS.Views.Faculty
{
    public partial class ColumnDialog : Window
    {
        public string ColumnName { get; private set; } = string.Empty;
        public int Points { get; private set; } = 1;

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMNCRP_DISABLED = 1;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public ColumnDialog(string title, string defaultName = "", int defaultPoints = 1)
        {
            InitializeComponent();
            SourceInitialized += (s, e) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int policy = DWMNCRP_DISABLED;
                DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
            };

            // Set the title
            TitleText.Text = title;

            // Set default values
            NameBox.Text = defaultName;
            PointsBox.Text = defaultPoints.ToString();

            // Focus and select all text when dialog opens
            this.Loaded += (s, e) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a column name.", "Missing Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                NameBox.SelectAll();
                return;
            }

            if (!int.TryParse(PointsBox.Text, out int points) || points < 0)
            {
                MessageBox.Show("Please enter a valid number for points.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                PointsBox.Focus();
                PointsBox.SelectAll();
                return;
            }

            ColumnName = name;
            Points = points;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Allow Enter key to trigger Save
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter)
            {
                SaveButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(this, new RoutedEventArgs());
            }
        }
    }
}