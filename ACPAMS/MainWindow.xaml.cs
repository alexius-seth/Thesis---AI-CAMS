using System.Windows;

namespace ACPAMS
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            // Start at Login
            ShowPage(new Views.Auth.LoginPage());
        }

        // Navigate to any UserControl or Page
        public static void Navigate(object content)
        {
            Instance?.ShowPage(content);
        }

        public static void Resize(double width, double height)
        {
            if (Instance == null) return;
            Instance.Width = width;
            Instance.Height = height;
            Instance.Left = (SystemParameters.PrimaryScreenWidth - width) / 2;
            Instance.Top = (SystemParameters.PrimaryScreenHeight - height) / 2;
        }

        private void ShowPage(object content)
        {
            MainContent.Content = content;
        }
    }
}