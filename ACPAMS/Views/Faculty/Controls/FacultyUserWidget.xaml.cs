using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ACPAMS.Views.Faculty.Controls
{
    public partial class FacultyUserWidget : UserControl
    {
        private readonly NotificationService _notificationService = new();
        private readonly DispatcherTimer _pollTimer;

        public FacultyUserWidget()
        {
            InitializeComponent();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _pollTimer.Tick += async (s, e) => await RefreshNotificationsAsync();

            Loaded += FacultyUserWidget_Loaded;
            Unloaded += FacultyUserWidget_Unloaded;
        }

        private async void FacultyUserWidget_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAvatarDisplay();
            SessionManager.AvatarChanged += UpdateAvatarDisplay;

            _pollTimer.Start();
            await RefreshNotificationsAsync();
        }

        private void FacultyUserWidget_Unloaded(object sender, RoutedEventArgs e)
        {
            SessionManager.AvatarChanged -= UpdateAvatarDisplay;
            _pollTimer.Stop();
        }

        public void UpdateAvatarDisplay()
        {
            if (SessionManager.CachedAvatar != null)
            {
                SidebarAvatarBrush.ImageSource = SessionManager.CachedAvatar;
                SidebarAvatarImageBorder.Visibility = Visibility.Visible;
                SidebarInitialsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                SidebarAvatarImageBorder.Visibility = Visibility.Collapsed;
                SidebarInitialsText.Text = SessionManager.Initials;
                SidebarInitialsText.Visibility = Visibility.Visible;
            }
        }

        private async Task RefreshNotificationsAsync()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            try
            {
                int count = await _notificationService.GetUnreadCountAsync(user.UserId);
                if (count > 0)
                {
                    BellBadgeCount.Text = count > 99 ? "99+" : count.ToString();
                    BellBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    BellBadge.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private async void BellButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationPopup.IsOpen = !NotificationPopup.IsOpen;
            if (NotificationPopup.IsOpen)
            {
                await LoadNotificationListAsync();
            }
        }

        private async Task LoadNotificationListAsync()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            NotificationItemsPanel.Children.Clear();

            try
            {
                var items = await _notificationService.GetNotificationsAsync(user.UserId);
                if (items.Count == 0)
                {
                    NotificationItemsPanel.Children.Add(new TextBlock
                    {
                        Text = "No notifications yet",
                        Foreground = Brushes.Gray,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20)
                    });
                    return;
                }

                foreach (var item in items)
                {
                    var rowBorder = new Border
                    {
                        BorderBrush = (Brush)FindResource("Border.Subtle"),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(12, 10, 12, 10),
                        Background = item.BackgroundBrush,
                        Cursor = Cursors.Hand,
                        Tag = item
                    };

                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var stripe = new Border
                    {
                        Background = item.SeverityBrush,
                        CornerRadius = new CornerRadius(2)
                    };
                    Grid.SetColumn(stripe, 0);
                    rowGrid.Children.Add(stripe);

                    var contentStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };

                    var topDock = new DockPanel();
                    var titleText = new TextBlock
                    {
                        Text = item.Title,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Primary")
                    };
                    var timeText = new TextBlock
                    {
                        Text = item.TimeAgo,
                        FontSize = 10,
                        Foreground = (Brush)FindResource("Text.Muted"),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    topDock.Children.Add(timeText);
                    topDock.Children.Add(titleText);
                    contentStack.Children.Add(topDock);

                    var msgText = new TextBlock
                    {
                        Text = item.Message,
                        FontSize = 11,
                        Foreground = (Brush)FindResource("Text.Secondary"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    contentStack.Children.Add(msgText);

                    Grid.SetColumn(contentStack, 1);
                    rowGrid.Children.Add(contentStack);

                    rowBorder.Child = rowGrid;
                    rowBorder.MouseLeftButtonUp += NotificationItem_Click;

                    NotificationItemsPanel.Children.Add(rowBorder);
                }
            }
            catch (Exception ex)
            {
                NotificationItemsPanel.Children.Add(new TextBlock
                {
                    Text = $"Error: {ex.Message}",
                    Foreground = Brushes.Red,
                    FontSize = 11,
                    Margin = new Thickness(10)
                });
            }
        }

        private async void NotificationItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is NotificationDto item)
            {
                await _notificationService.MarkAsReadAsync(item.NotificationId);
                NotificationPopup.IsOpen = false;
                await RefreshNotificationsAsync();

                // Deep-link routing
                if (item.TargetRoute == "Monitoring" && Guid.TryParse(item.TargetEntityId, out var monExamId))
                {
                    MainWindow.Navigate(new MonitoringTab(monExamId));
                }
                else if (item.TargetRoute == "StudentReport" && Guid.TryParse(item.TargetEntityId, out var attemptId))
                {
                    MainWindow.Navigate(new StudentReportPage(attemptId));
                }
                else if (item.TargetRoute == "Grading" && Guid.TryParse(item.TargetEntityId, out var grExamId))
                {
                    MainWindow.Navigate(new GradingTab(grExamId));
                }
                else if (item.TargetRoute == "Reports")
                {
                    MainWindow.NavigateToReportsStatic();
                }
            }
        }

        private async void MarkAllNotificationsAsRead_Click(object sender, RoutedEventArgs e)
        {
            var user = SessionManager.CurrentUser;
            if (user != null)
            {
                await _notificationService.MarkAllAsReadAsync(user.UserId);
                await RefreshNotificationsAsync();
                await LoadNotificationListAsync();
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToSettingsStatic();
        }
    }
}

