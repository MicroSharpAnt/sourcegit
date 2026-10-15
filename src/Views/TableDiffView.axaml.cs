using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class TableDiffGrid : DataGrid
    {
        protected override Type StyleKeyOverride => typeof(DataGrid);
        public TableDiffGrid Partner { get; set; }

        public TableDiffGrid()
        {
            AutoGenerateColumns = false;
            IsReadOnly = true;
            CanUserSortColumns = false;
            CanUserReorderColumns = false;
            CanUserResizeColumns = false;
            HeadersVisibility = DataGridHeadersVisibility.All;
            GridLinesVisibility = DataGridGridLinesVisibility.All;
            RowHeight = 30;
            RowHeaderWidth = 64;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            VerticalScroll += (_, e) => Partner?.SyncScroll(false, e.NewValue);
            HorizontalScroll += (_, e) => Partner?.SyncScroll(true, e.NewValue);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _vertical = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");
            _horizontal = e.NameScope.Find<ScrollBar>("PART_HorizontalScrollbar");
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
            OldGrid.LoadingRow += (_, e) => SetRowHeader(e, true);
            NewGrid.LoadingRow += (_, e) => SetRowHeader(e, false);
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
        }

        private ViewModels.TableDiffContext _model;

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.TableDiffContext.Result) || sender is not ViewModels.TableDiffContext vm)
                return;
            BuildColumns(OldGrid, vm.Result?.ColumnCount ?? 0, true);
            BuildColumns(NewGrid, vm.Result?.ColumnCount ?? 0, false);
        }

        private static void SetRowHeader(DataGridRowEventArgs e, bool old)
        {
            if (e.Row.DataContext is not Models.TableDiffRow row)
                return;
            var line = old ? row.OldLine : row.NewLine;
            var marker = row.Kind switch { Models.TableChangeKind.Added => "+", Models.TableChangeKind.Deleted => "−", Models.TableChangeKind.Modified => "~", _ => "" };
            e.Row.Header = line == 0 ? "—" : $"{marker} {line}";
        }

        private static void BuildColumns(DataGrid grid, int count, bool old)
        {
            grid.Columns.Clear();
            for (var i = 0; i < count; i++)
            {
                var column = i;
                grid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = ViewModels.TableDiffContext.ColumnName(i),
                    Width = new DataGridLength(150),
                    CellTemplate = new FuncDataTemplate<Models.TableDiffRow>((row, _) =>
                    {
                        var kind = row.Cells[column];
                        var highlight = kind == Models.TableChangeKind.Modified ? _modified :
                            kind == Models.TableChangeKind.Added && !old ? _added : kind == Models.TableChangeKind.Deleted && old ? _deleted : Brushes.Transparent;
                        var cell = new Border
                        {
                            Background = highlight,
                            Child = new TextBlock
                            {
                                Text = Models.TableDiff.Value(old ? row.Old : row.New, column),
                                Margin = new Thickness(6, 0),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            },
                        };
                        ToolTip.SetTip(cell, $"{App.Text("Diff.Old")}: {Models.TableDiff.Value(row.Old, column)}\n{App.Text("Diff.New")}: {Models.TableDiff.Value(row.New, column)}");
                        return cell;
                    }),
                });
            }
        }

        private static readonly IBrush _added = new SolidColorBrush(Color.Parse("#4034C759"));
        private static readonly IBrush _deleted = new SolidColorBrush(Color.Parse("#40FF453A"));
        private static readonly IBrush _modified = new SolidColorBrush(Color.Parse("#50FFCC00"));
    }
}
