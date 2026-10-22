using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Models;
using SourceGit.ViewModels;
using SourceGit.Views;

internal static class TableCopyTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        Application.Current.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
        Application.Current.Resources["Text.Copy"] = "Copy";
        var longText = new string('长', 300);
        var preview = new TablePreview();
        var sheet = new TableSheet { Name = "first", ColumnCount = 3 };
        sheet.Rows.AddRange(new[] { new[] { "A", longText, "C" }, new[] { "D", "tab\tvalue", "line\nbreak" }, new[] { "G", "say \"hi\"", "" } });
        preview.Sheets.Add(sheet);
        var second = new TableSheet { Name = "second", ColumnCount = 1 };
        second.Rows.Add(new[] { "second sheet" });
        second.Rows.AddRange(Enumerable.Range(1, 100).Select(i => new[] { $"row {i}" }));
        preview.Sheets.Add(second);
        var view = new TablePreviewView { DataContext = preview };
        var window = new Window { Width = 1000, Height = 650, Content = view };
        window.Show();
        void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Point Cell(DataGrid grid, int row, int column)
        {
            Layout();
            var container = grid.GetVisualDescendants().OfType<DataGridRow>().Single(r => r.Index == row);
            var cell = container.GetVisualDescendants().OfType<DataGridCell>().ElementAt(column);
            return cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), window)!.Value;
        }
        void Click(DataGrid grid, int row, int column, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            var point = Cell(grid, row, column);
            window.MouseDown(point, MouseButton.Left, modifiers);
            window.MouseUp(point, MouseButton.Left, modifiers);
            Layout();
        }
        void Copy(string expected, RawInputModifiers modifier = RawInputModifiers.Control)
        {
            window.KeyPressQwerty(PhysicalKey.C, modifier);
            window.KeyReleaseQwerty(PhysicalKey.C, modifier);
            Layout();
            var actual = window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult();
            if (actual != expected)
                throw new Exception("Unexpected clipboard: " + System.Text.Json.JsonSerializer.Serialize(actual));
            Console.WriteLine("PASS: clipboard contains expected full cell rectangle");
        }
        try
        {
            Layout();
            var grid = view.FindControl<TableCopyGrid>("Table")!;
            Click(grid, 0, 1);
            Copy(longText);
            Copy(longText, RawInputModifiers.Meta);
            Click(grid, 0, 0);
            Click(grid, 1, 1, RawInputModifiers.Shift);
            Copy("A\t" + longText + "\nD\t\"tab\tvalue\"");
            // Reverse-direction drag and Excel escaping for multiline, quotes and empty cells.
            var start = Cell(grid, 2, 2);
            var end = Cell(grid, 1, 1);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            Copy("\"tab\tvalue\"\t\"line\nbreak\"\n\"say \"\"hi\"\"\"\t");
            var inside = Cell(grid, 1, 1);
            window.MouseDown(inside, MouseButton.Right);
            window.MouseUp(inside, MouseButton.Right);
            Layout();
            Check(grid.ContextMenu!.IsOpen, "right click opens copy menu");
            var menu = (MenuItem)grid.ContextMenu.Items[0]!;
            Check(menu.IsEnabled && Equals(menu.Header, "Copy"), "localized Copy menu enabled");
            window.Clipboard!.SetTextAsync("sentinel").GetAwaiter().GetResult();
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Layout();
            Check(window.Clipboard.TryGetTextAsync().GetAwaiter().GetResult() == "\"tab\tvalue\"\t\"line\nbreak\"\n\"say \"\"hi\"\"\"\t", "context menu preserves selected range");
            grid.ContextMenu.Close();
            view.FindControl<ListBox>("Sheets")!.SelectedIndex = 1;
            Layout();
            grid.Focus();
            window.Clipboard.SetTextAsync("unchanged").GetAwaiter().GetResult();
            Copy("unchanged");
            Click(grid, 0, 0);
            Copy("second sheet");
            grid.ScrollIntoView(second.Rows[80], grid.Columns[0]);
            Layout();
            Click(grid, 80, 0);
            Copy("row 80");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            Copy("row 80\nrow 81");
            grid.ScrollIntoView(second.Rows[0], grid.Columns[0]);
            Layout();
            Click(grid, 0, 0);
            Copy("second sheet");

            SourceGit.Native.OS.GitExecutable = "git";
            using var repo = new TestRepository();
            repo.Commit("table", "data.csv", "id,value\n1,old value\n2,unchanged\n3,deleted\n");
            File.WriteAllText(Path.Combine(repo.Path, "data.csv"), "id,value\n1,new value\n2,unchanged\n4,added\n");
            var model = new TableDiffContext(repo.Path, new DiffOption(new Change { Path = "data.csv", WorkTree = ChangeState.Modified }, true));
            var diff = new TableDiffView { DataContext = model };
            window.Content = diff;
            Layout();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (model.IsLoading && DateTime.UtcNow < deadline)
            {
                Layout();
                Thread.Sleep(10);
            }
            Layout();
            Check(model.Result != null, "load actual comparison");
            model.KeyIndex = 1;
            deadline = DateTime.UtcNow.AddSeconds(15);
            while (model.IsLoading && DateTime.UtcNow < deadline)
            {
                Layout();
                Thread.Sleep(10);
            }
            Layout();
            var left = diff.FindControl<TableDiffGrid>("OldGrid")!;
            var right = diff.FindControl<TableDiffGrid>("NewGrid")!;
            var index = model.Rows.ToList().FindIndex(r => TableDiff.Value(r.Old, 1) == "old value");
            Click(left, index, 1);
            Copy("old value");
            Click(right, index, 1);
            Copy("new value");
            var deleted = model.Rows.ToList().FindIndex(r => TableDiff.Value(r.Old, 1) == "deleted");
            var added = model.Rows.ToList().FindIndex(r => TableDiff.Value(r.New, 1) == "added");
            Click(left, deleted, 0);
            Click(left, added, 1, RawInputModifiers.Shift);
            Copy("\t\n3\tdeleted");
            Click(right, deleted, 0);
            Click(right, added, 1, RawInputModifiers.Shift);
            Copy("4\tadded\n\t");
            model.OnlyChanges = !model.OnlyChanges;
            Layout();
            right.Focus();
            window.Clipboard.SetTextAsync("filtered").GetAwaiter().GetResult();
            Copy("filtered");
        }
        finally { window.Close(); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
