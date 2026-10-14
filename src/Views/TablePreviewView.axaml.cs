using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class TablePreviewView : UserControl
    {
        public TablePreviewView()
        {
            InitializeComponent();
            Table.LoadingRow += (_, e) => e.Row.Header = (e.Row.Index + 1).ToString();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is not Models.TablePreview preview || Sheets == null)
                return;

            Raw.IsChecked = false;
            Raw.IsVisible = preview.RawText != null;
            RawText.DataContext = preview.RawText;
            Error.Text = preview.Error;
            Error.IsVisible = !string.IsNullOrEmpty(preview.Error);
            Truncated.IsVisible = preview.IsTruncated;
            ValuesOnly.IsVisible = preview.RawText == null && preview.Error == null;
            Sheets.ItemsSource = preview.Sheets.Select(x => x.Name).ToArray();
            Sheets.SelectedIndex = preview.Sheets.Count > 0 ? 0 : -1;
            UpdateSheet();
        }

        private void OnSheetChanged(object sender, SelectionChangedEventArgs e) => UpdateSheet();

        private void UpdateSheet()
        {
            if (Table == null || DataContext is not Models.TablePreview preview)
                return;

            Table.ItemsSource = null;
            Table.Columns.Clear();
            if (Sheets.SelectedIndex < 0 || Sheets.SelectedIndex >= preview.Sheets.Count)
            {
                Empty.IsVisible = preview.Error == null;
                return;
            }

            var sheet = preview.Sheets[Sheets.SelectedIndex];
            Empty.IsVisible = sheet.Rows.Count == 0;
            for (var i = 0; i < sheet.ColumnCount; i++)
            {
                var index = i;
                Table.Columns.Add(new DataGridTemplateColumn
                {
                    Header = ColumnName(i),
                    Width = new DataGridLength(140),
                    CellTemplate = new FuncDataTemplate<string[]>((row, _) =>
                    {
                        var value = index < row.Length ? row[index] : string.Empty;
                        var cell = new TextBlock
                        {
                            Text = value,
                            Margin = new Thickness(6, 0),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        };
                        ToolTip.SetTip(cell, value);
                        return cell;
                    }),
                });
            }
            Table.ItemsSource = sheet.Rows;
        }

        private void OnRawChanged(object sender, RoutedEventArgs e)
        {
            if (Table == null)
                return;
            var raw = Raw.IsChecked == true;
            Table.IsVisible = !raw;
            RawText.IsVisible = raw;
            Sheets.IsEnabled = !raw;
        }

        private static string ColumnName(int index)
        {
            var name = string.Empty;
            for (var n = index + 1; n > 0; n = (n - 1) / 26)
                name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }
    }
}
