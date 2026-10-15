using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Views;

internal static class TableScrollTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        Application.Current.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
        var rows = Enumerable.Range(0, 100).Select(i => new Row(i)).ToArray();
        var left = CreateGrid(rows);
        var right = CreateGrid(rows);
        left.Partner = right;
        right.Partner = left;
        var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        panel.Children.Add(left);
        panel.Children.Add(right);
        Grid.SetColumn(right, 1);
        var window = new Window { Width = 900, Height = 400, Content = panel };
        window.Show();
        void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Layout();
        void Check(string scenario)
        {
            Layout();
            foreach (var name in new[] { "PART_VerticalScrollbar", "PART_HorizontalScrollbar" })
            {
                var a = Bar(left, name).Value;
                var b = Bar(right, name).Value;
                if (Math.Abs(a - b) > .1)
                    throw new Exception($"{scenario}: {name} left={a}, right={b}");
            }
            // Compare realized row identities and screen positions, not just thumb values.
            var aRows = VisibleRows(left);
            var bRows = VisibleRows(right);
            if (aRows.Length == 0 || !aRows.SequenceEqual(bRows))
                throw new Exception($"{scenario}: visible rows differ: {string.Join(",", aRows)} / {string.Join(",", bRows)}");
            Console.WriteLine($"PASS: {scenario}");
        }
        try
        {
            Check("initial layout");
            for (var i = 0; i < 3; i++)
            {
                window.MouseWheel(new Point(200, 150), new Vector(0, -3));
                Check($"left wheel {i + 1}");
            }
            window.MouseWheel(new Point(650, 150), new Vector(0, 2));
            Check("right wheel up");
            window.MouseWheel(new Point(650, 150), new Vector(-3, 0));
            Check("right horizontal wheel");
            window.MouseWheel(new Point(200, 150), new Vector(2, 0));
            Check("left horizontal wheel");
            right.SelectedIndex = 30;
            right.ScrollIntoView(rows[30], right.Columns[6]);
            Check("reveal selected row and column");
            right.Focus();
            window.KeyPressQwerty(Avalonia.Input.PhysicalKey.PageDown, Avalonia.Input.RawInputModifiers.None);
            window.KeyReleaseQwerty(Avalonia.Input.PhysicalKey.PageDown, Avalonia.Input.RawInputModifiers.None);
            Check("keyboard page down");
            var bar = Bar(left, "PART_VerticalScrollbar");
            bar.Value = 1234;
            bar.RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent });
            Check("drag left vertical thumb");
            bar = Bar(right, "PART_HorizontalScrollbar");
            bar.Value = 280;
            bar.RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent });
            Check("drag right horizontal thumb");
            window.MouseWheel(new Point(200, 150), new Vector(0, -100));
            Check("bottom boundary");
            window.Width = 1100;
            window.Height = 500;
            Check("resize at bottom");
            left.ItemsSource = rows.Take(10).ToArray();
            right.ItemsSource = left.ItemsSource;
            Check("filter rows while scrolled");
        }
        finally { window.Close(); }
    }

    private static TableDiffGrid CreateGrid(Row[] rows)
    {
        var grid = new TableDiffGrid { ItemsSource = rows };
        for (var i = 0; i < 8; i++)
            grid.Columns.Add(new DataGridTextColumn { Header = i, Width = new DataGridLength(150), Binding = new Binding(nameof(Row.Id)) });
        return grid;
    }

    private static ScrollBar Bar(TableDiffGrid grid, string name) => grid.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == name);

    private static string[] VisibleRows(TableDiffGrid grid) => grid.GetVisualDescendants().OfType<DataGridRow>()
        .Where(x => x.IsVisible && x.DataContext is Row)
        .Select(x => $"{((Row)x.DataContext).Id}@{Math.Round(x.TranslatePoint(default, grid)?.Y ?? -1, 2)}")
        .OrderBy(x => x).ToArray();

    public record Row(int Id);
}
