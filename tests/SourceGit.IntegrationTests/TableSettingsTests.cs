using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using SourceGit.Models;
using SourceGit.ViewModels;
using SourceGit.Views;

internal static class TableSettingsTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        SourceGit.Native.OS.GitExecutable = "git";
        using var repo = new TestRepository();
        repo.Commit("initial", "data.csv", "id,value\n1,old\n2,same\n");
        File.WriteAllText(Path.Combine(repo.Path, "data.csv"), "id,value\n1,new\n2,same\n");
        TableDiffContext NewModel() => new(repo.Path, new DiffOption(new Change { Path = "data.csv", WorkTree = ChangeState.Modified }, true));
        void Load(TableDiffContext model)
        {
            var load = model.LoadAsync();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while ((!load.IsCompleted || model.IsLoading) && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            load.GetAwaiter().GetResult();
            Check(model.Result != null && !model.IsLoading, "table loaded");
        }
        var first = NewModel();
        var second = NewModel();
        try
        {
            Load(first);
            first.OnlyChanges = true;
            Load(second);
            Check(second.OnlyChanges && second.Rows.Count == 1 && second.Rows[0].Kind == TableChangeKind.Modified, "new comparison inherits filter and hides unchanged rows");
            second.OnlyChanges = false;
            Check(!first.OnlyChanges && first.Rows.Count == 3, "existing comparison synchronizes filter off");
            second.Close();
            first.OnlyChanges = true;
            Load(second);
            Check(second.OnlyChanges && second.Rows.Count == 1, "reopened comparison inherits current filter");
        }
        finally
        {
            first.Close();
            second.Close();
            TableViewSettings.Instance.OnlyChanges = false;
        }

        TablePreview Csv() => TablePreview.Read("data.csv", new MemoryStream(Encoding.UTF8.GetBytes("id,value\n1,a\n")));
        var left = new TablePreviewView { DataContext = Csv() };
        var right = new TablePreviewView { DataContext = Csv() };
        var panel = new StackPanel();
        panel.Children.Add(left);
        panel.Children.Add(right);
        var window = new Window { Content = panel };
        window.Show();
        try
        {
            left.FindControl<CheckBox>("Raw").IsChecked = true;
            Check(right.FindControl<CheckBox>("Raw").IsChecked == true && !right.FindControl<DataGrid>("Table").IsVisible, "open CSV previews synchronize raw mode");
            right.DataContext = new TablePreview();
            Check(right.FindControl<DataGrid>("Table").IsVisible && !right.FindControl<CheckBox>("Raw").IsVisible, "XLSX remains a table");
            right.DataContext = Csv();
            Check(right.FindControl<CheckBox>("Raw").IsChecked == true, "CSV mode survives switching through XLSX");
            var third = new TablePreviewView { DataContext = Csv() };
            panel.Children.Add(third);
            Check(third.FindControl<CheckBox>("Raw").IsChecked == true, "new CSV preview inherits raw mode");
            third.FindControl<CheckBox>("Raw").IsChecked = false;
            Check(left.FindControl<DataGrid>("Table").IsVisible && right.FindControl<DataGrid>("Table").IsVisible, "switching back to table synchronizes all CSV previews");
        }
        finally
        {
            window.Close();
            TableViewSettings.Instance.ShowCsvRawText = false;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
