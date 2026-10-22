using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class TableDiffGrid : TableCopyGrid
    {
        protected override Type StyleKeyOverride => typeof(DataGrid);
        public TableDiffGrid Partner { get; set; }

        public TableDiffGrid()
        {
            AutoGenerateColumns = false;
            IsReadOnly = true;
            CanUserSortColumns = false;
            CanUserReorderColumns = false;
            CanUserResizeColumns = true;
            MinColumnWidth = 40;
            HeadersVisibility = DataGridHeadersVisibility.All;
            GridLinesVisibility = DataGridGridLinesVisibility.All;
            RowHeight = 30;
            RowHeaderWidth = 64;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            if (_vertical != null)
                _vertical.PropertyChanged -= OnScrollPositionChanged;
            if (_horizontal != null)
                _horizontal.PropertyChanged -= OnScrollPositionChanged;
            base.OnApplyTemplate(e);
            _vertical = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");
            _horizontal = e.NameScope.Find<ScrollBar>("PART_HorizontalScrollbar");
            if (_vertical != null)
                _vertical.PropertyChanged += OnScrollPositionChanged;
            if (_horizontal != null)
                _horizontal.PropertyChanged += OnScrollPositionChanged;
        }

        private void OnScrollPositionChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            // DataGrid's wheel Scroll event reports a delta before layout. The
            // scrollbar value is the committed offset, including keyboard scrolling.
            if (e.Property == RangeBase.ValueProperty && !_syncing && sender is ScrollBar bar)
                Partner?.SyncScroll(bar == _horizontal, bar.Value);
        }

        private void SyncScroll(bool horizontal, double value)
        {
            var bar = horizontal ? _horizontal : _vertical;
            if (_syncing || Partner?._syncing == true || bar == null || Math.Abs(bar.Value - value) < 0.1)
                return;
            _syncing = true;
            try
            {
                bar.Value = value;
                // DataGrid listens to Scroll, not Value changes. The public thumb event
                // lets ScrollBar notify its owner without accessing DataGrid internals.
                bar.RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent });
            }
            finally { _syncing = false; }
        }

        private bool _syncing;
        private ScrollBar _vertical;
        private ScrollBar _horizontal;
    }

    public partial class TableDiffView : UserControl
    {
        public TableDiffView()
        {
            InitializeComponent();
            OldGrid.Partner = NewGrid;
            NewGrid.Partner = OldGrid;
            OldGrid.CellValue = (item, column) => Models.TableDiff.Value(((Models.TableDiffRow)item).Old, column);
            NewGrid.CellValue = (item, column) => Models.TableDiff.Value(((Models.TableDiffRow)item).New, column);
            OldGrid.LoadingRow += (_, e) => SetRowHeader(e, true);
            NewGrid.LoadingRow += (_, e) => SetRowHeader(e, false);
            OldGrid.LoadingRow += (_, e) => SetRowHeight(e.Row);
            NewGrid.LoadingRow += (_, e) => SetRowHeight(e.Row);
            AddHandler(PointerPressedEvent, OnHeaderPressed, RoutingStrategies.Tunnel);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            BindModel();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ReleaseModel();
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (this.GetVisualRoot() != null)
                BindModel();
        }

        private async void BindModel()
        {
            if (_model == DataContext)
                return;
            ReleaseModel();
            if (DataContext is ViewModels.TableDiffContext vm)
            {
                _model = vm;
                vm.PropertyChanged += OnModelChanged;
                await vm.LoadAsync();
            }
        }

        private void ReleaseModel()
        {
            if (_model == null)
                return;
            _model.PropertyChanged -= OnModelChanged;
            _model.Close();
            _model = null;
            _rowHeights.Clear();
        }

        private ViewModels.TableDiffContext _model;

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not ViewModels.TableDiffContext vm)
                return;
            if (e.PropertyName == nameof(ViewModels.TableDiffContext.WrapText))
            {
                RefreshRowHeights();
                return;
            }
            if (e.PropertyName != nameof(ViewModels.TableDiffContext.Result))
                return;
            _rowHeights.Clear();
            BuildColumns(OldGrid, vm.Result?.ColumnCount ?? 0, true);
            BuildColumns(NewGrid, vm.Result?.ColumnCount ?? 0, false);
            RefreshRowHeights();
        }

        private static void SetRowHeader(DataGridRowEventArgs e, bool old)
        {
            if (e.Row.DataContext is not Models.TableDiffRow row)
                return;
            var line = old ? row.OldLine : row.NewLine;
            var marker = row.Kind switch { Models.TableChangeKind.Added => "+", Models.TableChangeKind.Deleted => "−", Models.TableChangeKind.Modified => "~", _ => "" };
            e.Row.Header = line == 0 ? "—" : $"{marker} {line}";
        }

        private void BuildColumns(DataGrid grid, int count, bool old)
        {
            foreach (var previous in grid.Columns)
                previous.PropertyChanged -= OnColumnChanged;
            grid.Columns.Clear();
            for (var i = 0; i < count; i++)
            {
                var column = i;
                var definition = new DataGridTemplateColumn
                {
                    Header = ViewModels.TableDiffContext.ColumnName(i),
                    Width = new DataGridLength(150),
                    CellTemplate = new FuncDataTemplate<Models.TableDiffRow>((row, _) =>
                    {
                        var kind = row.Cells[column];
                        var highlight = kind == Models.TableChangeKind.Modified ? _modified :
                            kind == Models.TableChangeKind.Added && !old ? _added : kind == Models.TableChangeKind.Deleted && old ? _deleted : Brushes.Transparent;
                        var text = new TextBlock
                        {
                            Text = Models.TableDiff.Value(old ? row.Old : row.New, column),
                            Margin = new Thickness(6, 0),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        };
                        text.Bind(TextBlock.TextWrappingProperty, new Binding(nameof(ViewModels.TableDiffContext.WrapText))
                        {
                            Source = _model,
                            Converter = _wrapConverter,
                        });
                        var cell = new Border
                        {
                            Background = highlight,
                            Child = text,
                        };
                        ToolTip.SetTip(cell, $"{App.Text("Diff.Old")}: {Models.TableDiff.Value(row.Old, column)}\n{App.Text("Diff.New")}: {Models.TableDiff.Value(row.New, column)}");
                        return TableCopyGrid.WrapCell(cell, column);
                    }),
                };
                definition.PropertyChanged += OnColumnChanged;
                grid.Columns.Add(definition);
            }
        }

        private void OnColumnChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_resizing || e.Property != DataGridColumn.WidthProperty || sender is not DataGridColumn column)
                return;
            var index = OldGrid.Columns.IndexOf(column);
            var partner = NewGrid;
            if (index < 0)
            {
                index = NewGrid.Columns.IndexOf(column);
                partner = OldGrid;
            }
            if (index < 0 || index >= partner.Columns.Count)
                return;
            _resizing = true;
            try { partner.Columns[index].Width = column.Width; }
            finally { _resizing = false; }
            RefreshRowHeights();
        }

        private void OnHeaderPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _model?.Result == null)
                return;
            var header = (e.Source as Visual)?.FindAncestorOfType<DataGridColumnHeader>(true);
            var grid = header?.FindAncestorOfType<TableDiffGrid>();
            if (grid == null)
                return;
            var index = grid.Columns.ToList().FindIndex(c => Equals(c.Header, header.Content));
            var x = e.GetPosition(header).X;
            if (x <= 5)
                index--;
            else if (x < header.Bounds.Width - 5)
                return;
            if (index < 0 || index >= grid.Columns.Count)
                return;
            var text = CreateMeasureText(grid);
            text.Text = grid.Columns[index].Header?.ToString();
            text.Measure(Size.Infinity);
            var width = text.DesiredSize.Width + 24;
            foreach (var row in _model.Result.Rows)
            {
                foreach (var values in new[] { row.Old, row.New })
                {
                    text.Text = Models.TableDiff.Value(values, index);
                    text.Measure(Size.Infinity);
                    width = Math.Max(width, text.DesiredSize.Width + 14);
                }
            }
            grid.Columns[index].Width = new DataGridLength(Math.Max(grid.MinColumnWidth, width));
            e.Handled = true;
        }

        private static TextBlock CreateMeasureText(DataGrid grid) => new()
        {
            FontFamily = grid.FontFamily,
            FontSize = grid.FontSize,
            FontStyle = grid.FontStyle,
            FontWeight = grid.FontWeight,
        };

        private void SetRowHeight(DataGridRow container)
        {
            if (_model?.WrapText != true || container.DataContext is not Models.TableDiffRow row)
            {
                container.Height = 30;
                return;
            }
            if (!_rowHeights.TryGetValue(row, out var height))
            {
                height = 30;
                var text = CreateMeasureText(OldGrid);
                text.TextWrapping = TextWrapping.Wrap;
                for (var i = 0; i < OldGrid.Columns.Count; i++)
                {
                    var width = Math.Max(1, OldGrid.Columns[i].ActualWidth - 14);
                    foreach (var values in new[] { row.Old, row.New })
                    {
                        text.Text = Models.TableDiff.Value(values, i);
                        text.Measure(new Size(width, double.PositiveInfinity));
                        height = Math.Max(height, Math.Ceiling(text.DesiredSize.Height) + 8);
                    }
                }
                _rowHeights[row] = height;
            }
            container.Height = height;
        }

        private void RefreshRowHeights()
        {
            _rowHeights.Clear();
            if (_heightRefreshPending)
                return;
            _heightRefreshPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _heightRefreshPending = false;
                OldGrid.RowHeight = NewGrid.RowHeight = double.NaN;
                foreach (var row in OldGrid.GetVisualDescendants().OfType<DataGridRow>())
                    SetRowHeight(row);
                foreach (var row in NewGrid.GetVisualDescendants().OfType<DataGridRow>())
                    SetRowHeight(row);
            }, DispatcherPriority.Loaded);
        }

        private bool _resizing;
        private bool _heightRefreshPending;
        private readonly Dictionary<Models.TableDiffRow, double> _rowHeights = new();
        private static readonly Avalonia.Data.Converters.FuncValueConverter<bool, TextWrapping> _wrapConverter =
            new(wrap => wrap ? TextWrapping.Wrap : TextWrapping.NoWrap);

        private static readonly IBrush _added = new SolidColorBrush(Color.Parse("#4034C759"));
        private static readonly IBrush _deleted = new SolidColorBrush(Color.Parse("#40FF453A"));
        private static readonly IBrush _modified = new SolidColorBrush(Color.Parse("#50FFCC00"));
    }
}
