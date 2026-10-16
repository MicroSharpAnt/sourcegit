using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml.Styling;
using SourceGit.Models;
using SourceGit.ViewModels;

internal static class ConflictSourceTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        Application.Current.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://SourceGit/")) { Source = new Uri("avares://SourceGit/Resources/Locales/en_US.axaml") });
        SourceGit.Native.OS.GitExecutable = "git";
        using var git = new TestRepository();
        git.Commit("base", "a.txt", "base\n");
        git.Git("checkout", "-b", "feature");
        var source = git.Commit("incoming change", "a.txt", "feature\n");
        git.Git("checkout", "main");
        var current = git.Commit("current change", "a.txt", "main\n");
        try { git.Git("merge", "feature"); } catch (InvalidOperationException) { }
        var repo = new Repository(false, git.Path, Path.Combine(git.Path, ".git"));
        var head = new SourceGit.Commands.QuerySingleCommit(git.Path, "HEAD").GetResult();
        var labels = ConflictSources.Create(new MergeInProgress(repo), head, "main");
        Check(labels.Mine.Title.Contains("main") && labels.Theirs.Title.Contains("feature"), "merge titles identify branches");
        Check(labels.Mine.Hash == "…" + current[^8..] && labels.Theirs.Hash == "…" + source[^8..], "merge displays hash suffixes");
        Check(labels.Mine.UseLabel.Contains("main") && labels.Theirs.UseLabel.Contains("feature"), "actions identify the same branches");
        var fixedHash = new ConflictSource("ConflictSource.Current", "main", new Commit { SHA = "0123456789abcdef0123456789abcdefcafebabe" });
        Check(fixedHash.Hash == "…cafebabe", "hash uses last eight characters, not prefix");
        git.Git("merge", "--abort");
        git.Git("checkout", "-b", "replay", "main~1");
        git.Commit("safe replay", "safe.txt", "safe\n");
        var stopped = git.Commit("conflicting replay", "a.txt", "replay\n");
        try { git.Git("rebase", "main"); } catch (InvalidOperationException) { }
        head = new SourceGit.Commands.QuerySingleCommit(git.Path, "HEAD").GetResult();
        var rebase = new RebaseInProgress(repo);
        labels = ConflictSources.Create(rebase, head, null);
        Check(head.SHA != rebase.Onto.SHA && labels.Mine.Commit.SHA == head.SHA, "rebase left identifies HEAD after earlier successful replay, not original target");
        Check(labels.Theirs.Commit.SHA == stopped && labels.Theirs.Title.Contains("replay") && labels.Theirs.Title.Contains("Replaying"), "rebase right identifies the stopped commit and original branch");
        git.Git("rebase", "--abort");
        git.Git("checkout", "main");
        try { git.Git("cherry-pick", source); } catch (InvalidOperationException) { }
        head = new SourceGit.Commands.QuerySingleCommit(git.Path, "HEAD").GetResult();
        labels = ConflictSources.Create(new CherryPickInProgress(repo), head, "main");
        Check(labels.Theirs.Title.Contains("Cherry-picking") && labels.Theirs.Commit.SHA == source && labels.Mine.Commit.SHA == current, "cherry-pick identifies current and incoming commits");
        git.Git("cherry-pick", "--abort");
        var unknown = ConflictSources.Create(null, head, null);
        Check(unknown.Theirs.Hash == "" && unknown.Theirs.Title == "Applied stash or patch", "unknown source does not invent a commit");
        Check(unknown.Mine.Title == "Current content", "detached or unknown branch does not claim a named branch");

        File.WriteAllText(Path.Combine(git.Path, "a.txt"), "stash\n");
        git.Git("stash", "push");
        File.WriteAllText(Path.Combine(git.Path, "a.txt"), "local staged\n");
        git.Git("add", "a.txt");
        try { git.Git("stash", "apply"); } catch (InvalidOperationException) { }
        Check(git.Git("show", ":2:a.txt") == "local staged" && git.Git("show", "HEAD:a.txt") == "main", "stash fixture has local-side content different from HEAD");
        var stashLabels = ConflictSources.Create(null, head, "main");
        Check(stashLabels.Mine.Commit == null && stashLabels.Mine.Hash == "" && stashLabels.Mine.Title == "Current content", "stash local changes are not attributed to HEAD");

        Application.Current.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://SourceGit/")) { Source = new Uri("avares://SourceGit/Resources/Icons.axaml") });
        var editorModel = new MergeConflictEditor(repo, head, "a.txt");
        var editor = new SourceGit.Views.MergeConflictEditor { DataContext = editorModel };
        Dispatcher.UIThread.RunJobs();
        Check(editor.FindControl<TextBlock>("MineTitle").Text == editorModel.Mine.Title && editor.FindControl<TextBlock>("TheirsTitle").Text == editorModel.Theirs.Title, "editor headers bind to actual sources");
        Check((string)editor.FindControl<Button>("UseMineButton").Content == editorModel.Mine.UseLabel && (string)editor.FindControl<Button>("UseTheirsButton").Content == editorModel.Theirs.UseLabel, "editor actions bind to matching sources");
        editor.Close();
        var overviewModel = new Conflict(repo, new WorkingCopy(repo), new Change { Path = "a.txt" });
        var overview = new SourceGit.Views.Conflict { DataContext = overviewModel };
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (overviewModel.Mine == null && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Check(overviewModel.Mine != null && overview.FindControl<TextBlock>("MineTitle").Text == overviewModel.Mine.Title && overview.FindControl<TextBlock>("TheirsTitle").Text == overviewModel.Theirs.Title, "overview headers bind after asynchronous source loading");

    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
