using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Models;
using SourceGit.ViewModels;
using SourceGit.Views;

internal static class TableSizingTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        Application.Current.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
        SourceGit.Native.OS.GitExecutable = "git";
        using var repo = new TestRepository();
        var longText = string.Join(" ", Enumerable.Repeat("Long cell content", 12));
        repo.Commit("table", "data.csv", "id,value\n1," + longText + "\n2,short\n" +
            string.Join("\n", Enumerable.Range(3, 80).Select(i => $"{i},short")));
        File.WriteAllText(Path.Combine(repo.Path, "data.csv"), "id,value\n1,short\n2," + longText + "\n" +
            string.Join("\n", Enumerable.Range(3, 80).Select(i => $"{i},short")));
        var model = new TableDiffContext(repo.Path, new DiffOption(new Change { Path = "data.csv", WorkTree = ChangeState.Modified }, true));
        var view = new TableDiffView { DataContext = model };
        var window = new Window { Width = 1000, Height = 700, Content = view };
        window.Show();
        void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            Console.WriteLine("PASS: " + message);
        }
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (model.IsLoading && DateTime.UtcNow < deadline)
            {
                Layout();
                Thread.Sleep(10);
            }
            Layout();
            Check(model.Result != null, "load actual table comparison");
            var left = view.FindControl<TableDiffGrid>("OldGrid");
            var right = view.FindControl<TableDiffGrid>("NewGrid");
            Check(!model.WrapText && left.CanUserResizeColumns && right.CanUserResizeColumns, "resizing enabled and wrapping off by default");
            var header = left.GetVisualDescendants().OfType<DataGridColumnHeader>().Single(h => Equals(h.Content, "B"));
            var boundary = header.TranslatePoint(new Point(header.Bounds.Width - 1, header.Bounds.Height / 2), window)!.Value;
            window.MouseDown(boundary, Avalonia.Input.MouseButton.Left);
            window.MouseMove(boundary + new Vector(100, 0));
            window.MouseUp(boundary + new Vector(100, 0), Avalonia.Input.MouseButton.Left);
            Layout();
            Check(left.Columns[1].ActualWidth > 200 && Math.Abs(left.Columns[1].ActualWidth - right.Columns[1].ActualWidth) < 1,
                "drag header boundary resizes both versions");
            right.Columns[1].Width = new DataGridLength(100);
            Layout();
            Check(Math.Abs(left.Columns[1].ActualWidth - 100) < 1, "right resize synchronizes back to left");
            model.WrapText = true;
            Layout();
            void CheckRows(string scenario)
            {
                Layout();
                var a = left.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsVisible && r.DataContext is TableDiffRow).ToArray();
                var b = right.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsVisible && r.DataContext is TableDiffRow).ToArray();
                Check(a.Length > 0 && a.Length == b.Length && a.Zip(b).All(pair =>
                    ReferenceEquals(pair.First.DataContext, pair.Second.DataContext) &&
                    Math.Abs(pair.First.Bounds.Height - pair.Second.Bounds.Height) < 1 &&
                    Math.Abs(pair.First.TranslatePoint(default, left)!.Value.Y - pair.Second.TranslatePoint(default, right)!.Value.Y) < 1), scenario);
            }
            CheckRows("wrapped rows have matching heights and positions");
            var tall = left.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.DataContext is TableDiffRow row && row.OldLine == 2).Single().Bounds.Height;
            Check(tall > 60, "long old value expands both rows");
            left.Columns[1].Width = new DataGridLength(300);
            Layout();
            CheckRows("widening keeps rows aligned");
            var shorter = left.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.DataContext is TableDiffRow row && row.OldLine == 2).Single().Bounds.Height;
            Check(shorter < tall, "widening shrinks wrapped row height");
            Check(left.GetVisualDescendants().OfType<DataGridRow>().Single(r => r.DataContext is TableDiffRow row && row.NewLine == 3).Bounds.Height > 60,
                "long new value expands both rows");
            window.MouseWheel(new Point(200, 500), new Vector(0, -4));
            CheckRows("scrolling wrapped rows keeps positions aligned");
            window.MouseWheel(new Point(700, 500), new Vector(0, -100));
            CheckRows("wrapped scrolling to bottom stays aligned");
            window.MouseWheel(new Point(200, 500), new Vector(0, 100));
            CheckRows("wrapped scrolling back to top stays aligned");
            model.OnlyChanges = true;
            Layout();
            CheckRows("filter preserves wrapped alignment");
            model.WrapText = false;
            Layout();
            CheckRows("disabling wrap preserves alignment");
            Check(left.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.DataContext is TableDiffRow).All(r => r.Bounds.Height == 30), "disabling wrap restores compact rows");
            header = right.GetVisualDescendants().OfType<DataGridColumnHeader>().Single(h => Equals(h.Content, "B"));
            boundary = header.TranslatePoint(new Point(header.Bounds.Width - 1, header.Bounds.Height / 2), window)!.Value;
            // Explicit click count makes double-click timing deterministic in headless mode.
            header.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(header, new Avalonia.Input.Pointer(1, Avalonia.Input.PointerType.Mouse, true),
                window, boundary, 0, new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
                Avalonia.Input.KeyModifiers.None, 2));
            Layout();
            Check(right.Columns[1].ActualWidth > 300 && Math.Abs(left.Columns[1].ActualWidth - right.Columns[1].ActualWidth) < 1, "double click fits both versions including old long value");
        }
        finally { window.Close(); }
    }
}
