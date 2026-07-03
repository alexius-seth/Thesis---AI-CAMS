using ACPAMS.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ACPAMS.Models;

namespace ACPAMS.Views.Faculty
{
    public partial class AIRubricModal : Window, INotifyPropertyChanged
    {
        public string RubricScope { get; set; } = string.Empty;
        public string AIInstructions { get; set; } = string.Empty;
        public ObservableCollection<RubricRow> Rows { get; set; }
        public ObservableCollection<TierColumn> TierColumns { get; set; }
        public string FilePath { get; set; }

        public string QuestionTitle { get; set; } = "Test II — Coding (Java)";
        public string SectionName { get; set; } = "Test II";
        public string QuestionId { get; set; } = string.Empty;

        private readonly string[] _tierColors = new string[]
        {
            "#15803D",
            "#1976D2",
            "#B45309",
            "#C62828",
            "#1A202C"
        };

        private bool _isSorting = false;

        public AIRubricModal()
        {
            InitializeComponent();
            DataContext = this;

            TierColumns = new ObservableCollection<TierColumn>();
            Rows = new ObservableCollection<RubricRow>();

            Rows.CollectionChanged += OnRowsChanged;
            TierColumns.CollectionChanged += OnTiersChanged;

            if (FileDropZone != null)
            {
                FileDropZone.AllowDrop = true;
                FileDropZone.DragEnter += FileDropZone_DragEnter;
                FileDropZone.Drop += FileDropZone_Drop;
            }

            UpdateHeaderContext();
            UpdateFileChip(null);

            BuildMatrix();

            this.Loaded += (s, e) => ResetScopeButtons();
        }

        public RubricData GetRubricData()
        {
            var result = new RubricData
            {
                Description = "AI Rubric Configuration",
                AiInstructions = AIInstructions,
                TotalWeight = 100,
                Criteria = new List<RubricCriteriaData>()
            };

            foreach (var row in Rows)
            {
                var criteria = new RubricCriteriaData
                {
                    Name = row.Criteria,
                    Description = row.Criteria,
                    MaxScore = TierColumns.Sum(t => t.Points),
                    WeightPercentage = 100m / Rows.Count,
                    Levels = new List<RubricLevelData>()
                };

                foreach (var tier in TierColumns)
                {
                    string levelValue = tier.Name switch
                    {
                        "Excellent" => row.Excellent,
                        "Good" => row.Good,
                        "Poor" => row.Poor,
                        _ => string.Empty
                    };

                    criteria.Levels.Add(new RubricLevelData
                    {
                        Name = tier.Name,
                        ScorePercentage = tier.Points,
                        Descriptor = levelValue
                    });
                }

                result.Criteria.Add(criteria);
            }

            return result;
        }

        private void OnRowsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            BuildMatrix();
        }

        private void OnTiersChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_isSorting)
            {
                SortTiersByPoints();
                BuildMatrix();
            }
        }

        private void SortTiersByPoints()
        {
            if (_isSorting) return;
            _isSorting = true;

            try
            {
                var sorted = TierColumns.OrderByDescending(t => t.Points).ToList();

                for (int i = 0; i < sorted.Count && i < _tierColors.Length; i++)
                {
                    sorted[i].Color = _tierColors[i];
                }

                for (int i = _tierColors.Length; i < sorted.Count; i++)
                {
                    sorted[i].Color = "#64748B";
                }

                TierColumns.Clear();
                foreach (var tier in sorted)
                {
                    TierColumns.Add(tier);
                }
            }
            finally
            {
                _isSorting = false;
            }
        }

        private void ResetScopeButtons()
        {
            SingleQuestionScopeBtn.Style = (Style)FindResource("Btn.ScopeToggle");
            EntireSectionScopeBtn.Style = (Style)FindResource("Btn.ScopeToggle");

            UpdateScopeDot(SingleQuestionScopeBtn, false);
            UpdateScopeDot(EntireSectionScopeBtn, false);

            RubricScope = string.Empty;
        }

        private void BuildMatrix()
        {
            var matrixGrid = MatrixGrid;
            if (matrixGrid == null) return;

            matrixGrid.Children.Clear();
            matrixGrid.RowDefinitions.Clear();
            matrixGrid.ColumnDefinitions.Clear();

            if (TierColumns.Count == 0)
            {
                matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var emptyMessage = new TextBlock
                {
                    Text = "No columns defined. Click 'Add Column' to create one.",
                    FontSize = 14,
                    Foreground = (Brush)FindResource("Text.Muted"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(emptyMessage, 0);
                Grid.SetColumn(emptyMessage, 0);
                matrixGrid.Children.Add(emptyMessage);
                return;
            }

            matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            foreach (var tier in TierColumns)
                matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            matrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int colIndex = 0;

            var criteriaHeader = new Border
            {
                Background = (Brush)FindResource("Bg.Base"),
                BorderBrush = (Brush)FindResource("Border.Subtle"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(12, 10, 12, 10),
                Child = new TextBlock
                {
                    Text = "Criteria",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Text.Secondary")
                }
            };
            Grid.SetRow(criteriaHeader, 0);
            Grid.SetColumn(criteriaHeader, colIndex++);
            matrixGrid.Children.Add(criteriaHeader);

            foreach (var tier in TierColumns)
            {
                var color = (Color)ColorConverter.ConvertFromString(tier.Color);
                var bgBrush = new SolidColorBrush(color) { Opacity = 0.15 };

                var headerBorder = new Border
                {
                    Background = bgBrush,
                    BorderBrush = (Brush)FindResource("Border.Subtle"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(8, 8, 8, 8),
                };

                var stack = new StackPanel();

                var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                namePanel.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var nameText = new TextBlock
                {
                    Text = tier.Name,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                namePanel.Children.Add(nameText);
                stack.Children.Add(namePanel);

                var pointsPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var ptsText = new TextBlock
                {
                    Text = tier.Points + " pts",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center
                };
                pointsPanel.Children.Add(ptsText);
                stack.Children.Add(pointsPanel);

                var editBtn = new Button
                {
                    Style = (Style)FindResource("Btn.Ghost"),
                    Padding = new Thickness(2, 0, 2, 0),
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Tag = tier,
                    Cursor = Cursors.Hand,
                    Content = new TextBlock
                    {
                        Text = "✎",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(color)
                    }
                };
                editBtn.Click += EditColumn_Click;
                stack.Children.Add(editBtn);

                headerBorder.Child = stack;
                Grid.SetRow(headerBorder, 0);
                Grid.SetColumn(headerBorder, colIndex++);
                matrixGrid.Children.Add(headerBorder);
            }

            var deleteHeader = new Border
            {
                Background = (Brush)FindResource("Bg.Base"),
                BorderBrush = (Brush)FindResource("Border.Subtle"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock { Text = "", FontSize = 10, Foreground = (Brush)FindResource("Text.Muted"), HorizontalAlignment = HorizontalAlignment.Center }
            };
            Grid.SetRow(deleteHeader, 0);
            Grid.SetColumn(deleteHeader, colIndex++);
            matrixGrid.Children.Add(deleteHeader);

            int rowIndex = 1;
            foreach (var row in Rows)
            {
                matrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int cellCol = 0;

                var criteriaBox = new TextBox
                {
                    Style = (Style)FindResource("Input.CriteriaCell"),
                    MinHeight = 52,
                    Text = row.Criteria,
                    Tag = row,
                    Cursor = Cursors.IBeam
                };
                criteriaBox.TextChanged += (s, e) =>
                {
                    var r = criteriaBox.Tag as RubricRow;
                    if (r != null) r.Criteria = criteriaBox.Text;
                };
                Grid.SetRow(criteriaBox, rowIndex);
                Grid.SetColumn(criteriaBox, cellCol++);
                matrixGrid.Children.Add(criteriaBox);

                foreach (var tier in TierColumns)
                {
                    string desc = GetCellValue(row, tier.Name);
                    var cellBox = new TextBox
                    {
                        Style = (Style)FindResource("Input.Cell"),
                        MinHeight = 52,
                        Text = desc,
                        Tag = new { Row = row, Tier = tier.Name },
                        Cursor = Cursors.IBeam
                    };
                    cellBox.TextChanged += (s, e) =>
                    {
                        var data = (dynamic)((TextBox)s).Tag;
                        var r = data.Row as RubricRow;
                        string tierName = data.Tier;
                        SetCellValue(r, tierName, ((TextBox)s).Text);
                    };
                    Grid.SetRow(cellBox, rowIndex);
                    Grid.SetColumn(cellBox, cellCol++);
                    matrixGrid.Children.Add(cellBox);
                }

                var deleteBtn = new Button
                {
                    Style = (Style)FindResource("Btn.Ghost"),
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 28,
                    Height = 28,
                    Tag = row,
                    Content = new TextBlock
                    {
                        Text = "✕",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Muted")
                    },
                    Cursor = Cursors.Hand
                };
                deleteBtn.Click += (s, e) =>
                {
                    var r = deleteBtn.Tag as RubricRow;
                    if (r != null && Rows.Count > 1)
                        Rows.Remove(r);
                    else
                        MessageBox.Show("You must have at least one row.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                };
                Grid.SetRow(deleteBtn, rowIndex);
                Grid.SetColumn(deleteBtn, cellCol++);
                matrixGrid.Children.Add(deleteBtn);

                rowIndex++;
            }

            UpdateTotalScore();
        }

        private void EditColumn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var tier = btn.Tag as TierColumn;
            if (tier == null) return;

            var dialog = new ColumnDialog("Edit Column", tier.Name, tier.Points);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                tier.Name = dialog.ColumnName;
                tier.Points = dialog.Points;
                SortTiersByPoints();
                UpdateTotalScore();
                BuildMatrix();
            }
        }

        private string GetCellValue(RubricRow row, string tierName)
        {
            if (tierName == "Excellent") return row.Excellent;
            if (tierName == "Good") return row.Good;
            if (tierName == "Poor") return row.Poor;
            return "";
        }

        private void SetCellValue(RubricRow row, string tierName, string value)
        {
            if (tierName == "Excellent") row.Excellent = value;
            else if (tierName == "Good") row.Good = value;
            else if (tierName == "Poor") row.Poor = value;
        }

        private void UpdateTotalScore()
        {
            int totalPerRow = TierColumns.Sum(t => t.Points);
            int total = Rows.Count * totalPerRow;
            var summaryBlock = SummaryTextBlock;
            if (summaryBlock != null)
                summaryBlock.Text = "Maximum rubric score: " + total + " pts  (" + Rows.Count + " criteria × " + totalPerRow + " pts each) — AI will normalize to the question's assigned points.";
        }

        private void UpdateHeaderContext()
        {
            var contextBlock = HeaderContextTextBlock;
            if (contextBlock != null)
            {
                contextBlock.Inlines.Clear();
                contextBlock.Inlines.Add(new Run("Scope for:"));
                contextBlock.Inlines.Add(new Run(" " + QuestionTitle) { FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text.Secondary") });
            }
        }

        private void UpdateScopeDot(Button btn, bool isSelected)
        {
            if (btn == null) return;

            var content = btn.Content as StackPanel;
            if (content == null) return;

            Ellipse ellipse = null;
            foreach (var child in content.Children)
            {
                if (child is Ellipse e)
                {
                    ellipse = e;
                    break;
                }
            }

            if (ellipse == null) return;

            if (isSelected)
            {
                ellipse.Fill = (Brush)FindResource("Accent.Primary");
                ellipse.Stroke = (Brush)FindResource("Accent.Primary");
                ellipse.StrokeThickness = 1.5;
            }
            else
            {
                ellipse.Fill = System.Windows.Media.Brushes.Transparent;
                ellipse.Stroke = (Brush)FindResource("Border.Medium");
                ellipse.StrokeThickness = 1.5;
            }
        }

        private void ScopeToggle_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            SingleQuestionScopeBtn.Style = (Style)FindResource("Btn.ScopeToggle");
            EntireSectionScopeBtn.Style = (Style)FindResource("Btn.ScopeToggle");

            UpdateScopeDot(SingleQuestionScopeBtn, false);
            UpdateScopeDot(EntireSectionScopeBtn, false);

            btn.Style = (Style)FindResource("Btn.ScopeToggle.Active");
            UpdateScopeDot(btn, true);

            RubricScope = (btn == SingleQuestionScopeBtn) ? "Question" : "Section";
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            if (TierColumns.Count == 0)
            {
                MessageBox.Show("Please add at least one column first.", "No Columns", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newRow = new RubricRow
            {
                Criteria = "New Criteria " + (Rows.Count + 1),
                Excellent = "",
                Good = "",
                Poor = ""
            };

            foreach (var tier in TierColumns)
            {
                SetCellValue(newRow, tier.Name, "");
            }

            Rows.Add(newRow);
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ColumnDialog("Add Column", "Tier " + (TierColumns.Count + 1), 1);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                var newColumn = new TierColumn
                {
                    Name = dialog.ColumnName,
                    Color = "#64748B",
                    Points = dialog.Points
                };

                TierColumns.Add(newColumn);
                SortTiersByPoints();

                foreach (var row in Rows)
                {
                    SetCellValue(row, dialog.ColumnName, "");
                }

                BuildMatrix();
            }
        }

        private void FileDropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void FileDropZone_Drop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                FilePath = files[0];
                UpdateFileChip(FilePath);
            }
        }

        private void BrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf|PowerPoint (*.pptx)|*.pptx|Word (*.docx)|*.docx|Image (*.png)|*.png|All files (*.*)|*.*",
                Title = "Select Rubric Reference File"
            };
            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
                UpdateFileChip(FilePath);
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            FilePath = null;
            UpdateFileChip(null);
        }

        private void UpdateFileChip(string path)
        {
            var chip = FileChip;
            if (chip == null) return;

            if (string.IsNullOrEmpty(path))
            {
                chip.Visibility = Visibility.Collapsed;
            }
            else
            {
                chip.Visibility = Visibility.Visible;
                var fileNameBlock = chip.FindName("FileNameTextBlock") as TextBlock;
                var fileInfoBlock = chip.FindName("FileInfoTextBlock") as TextBlock;
                if (fileNameBlock != null)
                    fileNameBlock.Text = System.IO.Path.GetFileName(path);
                if (fileInfoBlock != null)
                {
                    var fi = new FileInfo(path);
                    string size = fi.Exists ? (fi.Length / 1024).ToString() + " KB" : "Unknown";
                    fileInfoBlock.Text = size + " · Uploaded";
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Rows.Count == 0)
            {
                MessageBox.Show("Please add at least one rubric row.", "Incomplete Rubric", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (TierColumns.Count == 0)
            {
                MessageBox.Show("Please add at least one rubric column.", "Incomplete Rubric", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(AIPromptTextBox?.Text))
            {
                MessageBox.Show("Please provide AI grading instructions.", "Incomplete Rubric", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(RubricScope))
            {
                MessageBox.Show("Please select the rubric scope (This question only or Entire test section).", "Incomplete Rubric", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            AIInstructions = AIPromptTextBox.Text;
            DialogResult = true;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public class RubricRow
        {
            public string Criteria { get; set; }
            public string Excellent { get; set; }
            public string Good { get; set; }
            public string Poor { get; set; }
        }

        public class TierColumn
        {
            public string Name { get; set; }
            public string Color { get; set; }
            public int Points { get; set; }
        }
    }
}