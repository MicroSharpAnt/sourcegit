using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    // Shared by the preview and each side of the table comparison. DataGrid's
    // built-in selection is row based; keep the rectangle in source coordinates.
    public class TableCopyGrid : DataGrid
    {
        protected override Type StyleKeyOverride => typeof(DataGrid);

        public Func<object, int, string> CellValue { get; set; }

        public TableCopyGrid()
        {
            AddHandler(PointerPressedEvent, OnCellPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, OnCellMoved, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, OnCellReleased, RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, OnCopyKeyDown, RoutingStrategies.Tunnel);
            PointerCaptureLost += (_, _) => _dragging = false;
            LayoutUpdated += (_, _) => UpdateSelectionVisuals();
            Columns.CollectionChanged += (_, _) => ClearCellSelection();
            var copy = new MenuItem();
            copy.Bind(MenuItem.HeaderProperty, this.GetResourceObservable("Text.Copy"));
            copy.Click += async (_, _) => await CopySelectionAsync();
            ContextMenu = new ContextMenu { ItemsSource = new[] { copy } };
            ContextMenu.Opening += (_, _) => copy.IsEnabled = _anchorRow >= 0;
        }

        public static Control WrapCell(Control content, int column) => new TableCopyCell
        {
            Column = column,
            Child = content,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
        };

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != ItemsSourceProperty)
                return;
            if (change.OldValue is INotifyCollectionChanged old)
                old.CollectionChanged -= OnRowsChanged;
            if (change.NewValue is INotifyCollectionChanged current)
                current.CollectionChanged += OnRowsChanged;
            ResetRows();
        }

        private void OnRowsChanged(object sender, NotifyCollectionChangedEventArgs e) => ResetRows();

        private void ResetRows()
        {
            _rows = ItemsSource?.Cast<object>().ToList() ?? [];
            ClearCellSelection();
        }

        private void ClearCellSelection()
        {
            _anchorRow = _endRow = -1;
            _dragging = false;
            UpdateSelectionVisuals();
        }

        private bool TryCell(Visual visual, out int row, out int column)
        {
            var cell = visual?.FindAncestorOfType<TableCopyCell>(true);
            var container = cell?.FindAncestorOfType<DataGridRow>();
            row = container?.Index ?? -1;
            column = cell?.Column ?? -1;
            return cell?.FindAncestorOfType<TableCopyGrid>() == this && row >= 0 && row < _rows.Count && column >= 0 && column < Columns.Count;
        }

        private bool IsSelected(int row, int column) => _anchorRow >= 0 &&
            row >= Math.Min(_anchorRow, _endRow) && row <= Math.Max(_anchorRow, _endRow) &&
            column >= Math.Min(_anchorColumn, _endColumn) && column <= Math.Max(_anchorColumn, _endColumn);

        private void SelectCell(int row, int column, bool extend)
        {
            if (!extend || _anchorRow < 0)
            {
                _anchorRow = row;
                _anchorColumn = column;
            }
            _endRow = row;
            _endColumn = column;
            UpdateSelectionVisuals();
        }

        private void OnCellPressed(object sender, PointerPressedEventArgs e)
        {
            if (!TryCell(e.Source as Visual, out var row, out var column))
                return;
            var properties = e.GetCurrentPoint(this).Properties;
            if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
                return;
            Focus();
            if (!properties.IsRightButtonPressed || !IsSelected(row, column))
                SelectCell(row, column, properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (properties.IsLeftButtonPressed)
            {
                _dragging = true;
                e.Pointer.Capture(this);
            }
            e.Handled = true;
        }

        private void OnCellMoved(object sender, PointerEventArgs e)
        {
            if (!_dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (TryCell(this.InputHitTest(e.GetPosition(this)) as Visual, out var row, out var column))
                SelectCell(row, column, true);
            e.Handled = true;
        }

        private void OnCellReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_dragging)
                return;
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private async void OnCopyKeyDown(object sender, KeyEventArgs e)
        {
            var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (command && e.Key == Key.C)
            {
                e.Handled = true;
                await CopySelectionAsync();
                return;
            }
            if (_rows.Count == 0 || Columns.Count == 0)
                return;
            if (command && e.Key == Key.A)
            {
                SelectCell(0, 0, false);
                SelectCell(_rows.Count - 1, Columns.Count - 1, true);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                ClearCellSelection();
                e.Handled = true;
                return;
            }
            var row = Math.Max(0, _endRow);
            var column = _anchorRow < 0 ? 0 : _endColumn;
            switch (e.Key)
            {
                case Key.Left: column--; break;
                case Key.Right: column++; break;
                case Key.Up: row--; break;
                case Key.Down: row++; break;
                case Key.Home: column = 0; if (command) row = 0; break;
                case Key.End: column = Columns.Count - 1; if (command) row = _rows.Count - 1; break;
                default: return;
            }
            row = Math.Clamp(row, 0, _rows.Count - 1);
            column = Math.Clamp(column, 0, Columns.Count - 1);
            SelectCell(row, column, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            ScrollIntoView(_rows[row], Columns[column]);
            e.Handled = true;
        }

        private async Task CopySelectionAsync()
        {
            if (_anchorRow < 0 || CellValue == null)
                return;
            var text = new StringBuilder();
            var firstRow = Math.Min(_anchorRow, _endRow);
            var firstColumn = Math.Min(_anchorColumn, _endColumn);
            for (var row = firstRow; row <= Math.Max(_anchorRow, _endRow); row++)
            {
                if (row > firstRow)
                    text.Append('\n');
                for (var column = firstColumn; column <= Math.Max(_anchorColumn, _endColumn); column++)
                {
                    if (column > firstColumn)
                        text.Append('\t');
                    var value = CellValue(_rows[row], column) ?? string.Empty;
                    // Excel's quoted TSV preserves tabs, newlines and quotes inside a cell.
                    if (value.IndexOfAny(['\t', '\r', '\n', '"']) >= 0)
                        text.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                    else
                        text.Append(value);
                }
            }
            await this.CopyTextAsync(text.ToString());
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var cell in this.GetVisualDescendants().OfType<TableCopyCell>())
            {
                var selected = TryCell(cell, out var row, out var column) && IsSelected(row, column);
                cell.BorderBrush = selected ? _selectionBrush : Brushes.Transparent;
            }
        }

        private sealed class TableCopyCell : Border
        {
            public int Column { get; init; }
        }

        private List<object> _rows = [];
        private int _anchorRow = -1;
        private int _anchorColumn;
        private int _endRow = -1;
        private int _endColumn;
        private bool _dragging;
        private static readonly IBrush _selectionBrush = new SolidColorBrush(Color.Parse("#409CFF"));
    }
}
