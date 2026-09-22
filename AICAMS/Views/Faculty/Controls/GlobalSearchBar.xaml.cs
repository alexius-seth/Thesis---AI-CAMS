using ACPAMS.Data;
using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ACPAMS.Views.Faculty.Controls
{
    public partial class GlobalSearchBar : UserControl
    {
        private readonly GlobalSearchService _searchService = new();
        private readonly DispatcherTimer _debounceTimer;

        public GlobalSearchBar()
        {
            InitializeComponent();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                await ExecuteSearchAsync();
            };

            Unloaded += (s, e) => _debounceTimer.Stop();
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void SearchInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
        }

        private async Task ExecuteSearchAsync()
        {
            string query = SearchInput.Text.Trim();
            if (query.Length < 2)
            {
                SearchPopup.IsOpen = false;
                return;
            }

            var results = await _searchService.SearchAsync(query);
            SearchResultsPanel.Children.Clear();

            if (results.Count == 0)
            {
                SearchResultsPanel.Children.Add(new TextBlock
                {
                    Text = "No matching assessments or students",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    Padding = new Thickness(14, 10, 14, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            else
            {
                foreach (var res in results)
                {
                    var itemBorder = new Border
                    {
                        Padding = new Thickness(12, 8, 12, 8),
                        Cursor = Cursors.Hand,
                        Background = Brushes.Transparent,
                        Tag = res
                    };

                    itemBorder.MouseEnter += (s, ev) => itemBorder.Background = (Brush)FindResource("Accent.PrimaryBg");
                    itemBorder.MouseLeave += (s, ev) => itemBorder.Background = Brushes.Transparent;
                    itemBorder.MouseLeftButtonUp += SearchResult_Click;

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var iconBlock = new TextBlock
                    {
                        Text = res.Icon,
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 14,
                        Foreground = (Brush)FindResource("Accent.Primary"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(iconBlock, 0);
                    grid.Children.Add(iconBlock);

                    var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    textStack.Children.Add(new TextBlock
                    {
                        Text = res.Title,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("Text.Primary")
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text = res.Subtitle,
                        FontSize = 11,
                        Foreground = (Brush)FindResource("Text.Muted")
                    });
                    Grid.SetColumn(textStack, 1);
                    grid.Children.Add(textStack);

                    var badge = new Border
                    {
                        Background = res.TypeBadgeBg,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = res.Type,
                            FontSize = 10,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = res.TypeBadgeFg
                        }
                    };
                    Grid.SetColumn(badge, 2);
                    grid.Children.Add(badge);

                    itemBorder.Child = grid;
                    SearchResultsPanel.Children.Add(itemBorder);
                }
            }

            SearchPopup.IsOpen = true;
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is SearchResultDto res)
            {
                SearchPopup.IsOpen = false;
                SearchInput.Text = string.Empty;

                if (res.TargetRoute == "Exam" && Guid.TryParse(res.TargetId, out var examId))
                {
                    MainWindow.NavigateToReportsStatic(examId);
                }
                else if (res.TargetRoute == "Student")
                {
                    MainWindow.NavigateToStudentProfileStatic(res.TargetId, NavigationOrigin.Search);
                }
            }
        }
    }
}
