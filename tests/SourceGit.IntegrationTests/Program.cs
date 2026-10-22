using System.Diagnostics;
using SourceGit.Commands;

if (args.Contains("--table-copy"))
{
    TableCopyTests.Run();
    return;
}

if (args.Contains("--file-manager"))
{
    MacOSFileManagerTests.Run();
    return;
}

if (args.Contains("--conflict-sources"))
{
    ConflictSourceTests.Run();
    return;
}

if (args.Contains("--table-settings"))
{
    TableSettingsTests.Run();
    return;
}

if (args.Contains("--table-sizing"))
{
    TableSizingTests.Run();
    return;
}

if (args.Contains("--table-scroll"))
{
    TableScrollTests.Run();
    return;
}

// Exercise the same editor entry points that the installed app uses during rebase.
if (args.Length > 0 && args[0].StartsWith("--rebase-", StringComparison.Ordinal))
{
    SourceGit.App.Main(args);
    return;
}

SourceGit.Native.OS.GitExecutable = OperatingSystem.IsWindows() ? "git.exe" : "git";
SourceGit.Native.OS.CredentialHelper = string.Empty;
Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sourcegit-tests-no-global-config"));
Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
await TableDiffTests.RunAsync();
if (args.Contains("--table-diff"))
    return;
await TablePreviewTests.RunAsync();
if (args.Contains("--table-preview"))
    return;
using var repo = new TestRepository();
var root = repo.Commit("root", "root.txt", "root");
var b = repo.Commit("B", "b.txt", "B");
var c = repo.Commit("C", "c.txt", "C");
var d = repo.Commit("D", "d.txt", "D");
var tree = repo.Git("rev-parse", "HEAD^{tree}");
var squash = new SquashCommits(repo.Path);
Check(await squash.PrepareAsync([c, b]), "prepare middle selection in any click order");
Check(squash.Message == "B\n\nC", "messages ordered oldest first");
Check(await squash.SquashAsync("BC\n\nCombined changes"), "execute middle squash");
Check(repo.Git("log", "--format=%s", "--reverse") == "root\nBC\nD", "only selected commits combined");
Check(repo.Git("rev-parse", "HEAD^{tree}") == tree, "final tree preserved");
Check(repo.Git("rev-parse", squash.RecoveryRef) == d, "recovery reference retains original tip");
Check(repo.Git("show", "-s", "--format=%B", "HEAD~") == "BC\n\nCombined changes", "custom message preserved");
Console.WriteLine("PASS: middle squash, message, tree and recovery reference");

using (var dirty = new TestRepository())
{
    dirty.Commit("root", "root.txt", "root");
    var first = dirty.Commit("first", "first.txt", "first");
    var last = dirty.Commit("last", "last.txt", "last");
    File.WriteAllText(System.IO.Path.Combine(dirty.Path, "untracked.txt"), "keep me");
    var operation = new SquashCommits(dirty.Path);
    Check(!await operation.PrepareAsync([first, last]), "reject untracked work before preview");
    Check(dirty.Git("rev-parse", "HEAD") == last, "dirty rejection preserves HEAD");
    Check(File.ReadAllText(System.IO.Path.Combine(dirty.Path, "untracked.txt")) == "keep me", "dirty rejection preserves file");
}
Console.WriteLine("PASS: dirty work is rejected without changes");

using (var changed = new TestRepository())
{
    changed.Commit("root", "root.txt", "root");
    var first = changed.Commit("first", "first.txt", "first");
    var last = changed.Commit("last", "last.txt", "last");
    var operation = new SquashCommits(changed.Path);
    Check(await operation.PrepareAsync([first, last]), "prepare before HEAD changes");
    var newer = changed.Commit("newer", "newer.txt", "newer");
    Check(!await operation.SquashAsync("combined"), "reject stale HEAD");
    Check(changed.Git("rev-parse", "HEAD") == newer, "stale rejection preserves newer commit");
    Check(changed.Git("for-each-ref", "refs/heads/sourcegit-backup").Length == 0, "rejected execution creates no backup");
}
Console.WriteLine("PASS: stale previews are rejected");

using (var empty = new TestRepository())
{
    empty.Commit("root", "root.txt", "root");
    var first = empty.Commit("first", "first.txt", "first");
    var last = empty.Commit("last", "last.txt", "last");
    var operation = new SquashCommits(empty.Path);
    Check(await operation.PrepareAsync([first, last]), "prepare before blank message");
    Check(!await operation.SquashAsync(" \n\t"), "reject blank message");
    Check(empty.Git("rev-parse", "HEAD") == last, "blank message preserves HEAD");
}
Console.WriteLine("PASS: blank messages are rejected");

foreach (var scenario in new[] { "nonconsecutive", "root", "detached", "foreign", "merge", "merge-in-selection", "pending-merge", "staged", "unstaged" })
{
    using var invalid = new TestRepository();
    var a = invalid.Commit("root", "root.txt", "root");
    var b1 = invalid.Commit("B", "b.txt", "B");
    var c1 = invalid.Commit("C", "c.txt", "C");
    var d1 = invalid.Commit("D", "d.txt", "D");
    string[] selection = [b1, c1];
    switch (scenario)
    {
        case "nonconsecutive":
            selection = [b1, d1];
            break;
        case "root":
            selection = [a, b1];
            break;
        case "detached":
            invalid.Git("checkout", "--detach");
            break;
        case "foreign":
            invalid.Git("checkout", "-b", "side", a);
            var x = invalid.Commit("X", "x.txt", "X");
            var y = invalid.Commit("Y", "y.txt", "Y");
            invalid.Git("checkout", "main");
            selection = [x, y];
            break;
        case "merge":
        case "merge-in-selection":
        case "pending-merge":
            invalid.Git("checkout", "-b", "side", b1);
            invalid.Git("commit", "--allow-empty", "-m", "side");
            invalid.Git("checkout", "main");
            invalid.Git("merge", "--no-ff", "--no-commit", "side");
            if (scenario != "pending-merge")
                invalid.Git("commit", "-m", "merge");
            if (scenario == "merge-in-selection")
                selection = [d1, invalid.Git("rev-parse", "HEAD")];
            break;
        case "staged":
        case "unstaged":
            File.WriteAllText(System.IO.Path.Combine(invalid.Path, "root.txt"), "changed");
            if (scenario == "staged")
                invalid.Git("add", "root.txt");
            break;
    }

    var head = invalid.Git("rev-parse", "HEAD");
    var status = invalid.Git("status", "--porcelain");
    var operation = new SquashCommits(invalid.Path);
    Check(!await operation.PrepareAsync(selection), "reject " + scenario);
    Check(invalid.Git("rev-parse", "HEAD") == head, "preserve HEAD: " + scenario);
    Check(invalid.Git("status", "--porcelain") == status, "preserve status: " + scenario);
    Console.WriteLine("PASS: reject " + scenario);
}

using (var stopped = new TestRepository())
{
    stopped.Commit("root", "root.txt", "root");
    var first = stopped.Commit("B", "b.txt", "B");
    var second = stopped.Commit("C", "c.txt", "C");
    var tip = stopped.Commit("D", "d.txt", "D");
    var operation = new SquashCommits(stopped.Path) { RaiseError = false };
    Check(await operation.PrepareAsync([first, second]), "prepare interrupted squash");
    stopped.Git("config", "commit.gpgsign", "true");
    stopped.Git("config", "gpg.format", "ssh");
    var signingKey = System.IO.Path.Combine(stopped.Path, ".git", "test-signing-key");
    stopped.Git("config", "user.signingkey", signingKey);
    Check(!await operation.SquashAsync("BC"), "signing failure stops rebase");
    Check(operation.IsRebasing, "failed squash retains resumable rebase");
    Check(stopped.Git("rev-parse", operation.RecoveryRef) == tip, "failed squash retains recovery reference");
    var keygen = new ProcessStartInfo("ssh-keygen") { UseShellExecute = false };
    foreach (var arg in new[] { "-q", "-t", "ed25519", "-N", "", "-f", signingKey })
        keygen.ArgumentList.Add(arg);
    using (var process = Process.Start(keygen)!)
    {
        process.WaitForExit();
        Check(process.ExitCode == 0, "repair test signing configuration");
    }

    // Introduce a real add/add conflict in the later commit while resolving the stop.
    File.WriteAllText(System.IO.Path.Combine(stopped.Path, "d.txt"), "resolution introduced this file");
    stopped.Git("add", "d.txt");
    // Git asks for an amend after a signing failure during squash.
    stopped.Git("commit", "--amend", "-m", "BC");
    var resume = new Command
    {
        WorkingDirectory = stopped.Path,
        Context = stopped.Path,
        Editor = Command.EditorType.RebaseEditor,
        Args = "-c core.commentChar=\"^\" -c core.commentString=\"±\" rebase --continue",
        RaiseError = false,
    };
    Check(!await resume.ExecAsync(), "later commit pauses with add/add conflict");
    Check(stopped.Git("diff", "--name-only", "--diff-filter=U") == "d.txt", "expected conflict is visible");
    File.WriteAllText(System.IO.Path.Combine(stopped.Path, "d.txt"), "D");
    stopped.Git("add", "d.txt");
    Check(await resume.ExecAsync(), "resume after resolving later conflict");
    var subjects = stopped.Git("log", "--format=%s", "--reverse");
    Check(subjects == "root\nBC\nD", "continuation preserves later commit message: " + subjects);
}
Console.WriteLine("PASS: interrupted squash and downstream conflict continue correctly");

foreach (var scenario in new[] { "late-dirty", "switched-branch", "abort" })
{
    using var guard = new TestRepository();
    guard.Commit("root", "root.txt", "root");
    var first = guard.Commit("first", "first.txt", "first");
    var last = guard.Commit("last", "last.txt", "last");
    var operation = new SquashCommits(guard.Path) { RaiseError = false };
    Check(await operation.PrepareAsync([first, last]), "prepare " + scenario);
    if (scenario == "late-dirty")
        File.WriteAllText(System.IO.Path.Combine(guard.Path, "first.txt"), "unsaved work");
    else if (scenario == "switched-branch")
        guard.Git("checkout", "-b", "other");
    else
    {
        guard.Git("config", "commit.gpgsign", "true");
        guard.Git("config", "gpg.program", "sourcegit-test-missing-signer");
    }

    Check(!await operation.SquashAsync("combined"), "reject or stop " + scenario);
    if (scenario == "abort")
    {
        Check(operation.IsRebasing, "abort has in-progress operation");
        guard.Git("rebase", "--abort");
        Check(!operation.IsRebasing, "abort leaves no active rebase");
        Check(guard.Git("status", "--porcelain").Length == 0, "abort restores clean worktree");
        Check(guard.Git("rev-parse", operation.RecoveryRef) == last, "abort preserves backup");
    }
    else if (scenario == "late-dirty")
        Check(File.ReadAllText(System.IO.Path.Combine(guard.Path, "first.txt")) == "unsaved work", "late dirty work preserved");
    Check(guard.Git("rev-parse", "HEAD") == last, "preserve tip for " + scenario);
    Console.WriteLine("PASS: " + scenario);
}

using (var tail = new TestRepository())
{
    tail.Commit("root", "file.txt", "original");
    var first = tail.Commit("change", "file.txt", "changed");
    tail.Git("commit", "--amend", "--no-edit", "--author", "Original Author <author@example.invalid>");
    first = tail.Git("rev-parse", "HEAD");
    var second = tail.Commit("restore", "file.txt", "original");
    tail.Git("commit", "--allow-empty", "-m", "empty");
    var last = tail.Git("rev-parse", "HEAD");
    tail.Git("branch", "keep-this-tip");
    tail.Git("config", "rebase.updateRefs", "true");
    tail.Git("config", "rebase.autoSquash", "true");
    var operation = new SquashCommits(tail.Path);
    Check(await operation.PrepareAsync([last, first, second]), "prepare three tail commits");
    Check(await operation.SquashAsync("Empty combined change\n\n# literal message"), "squash cancelling and empty changes");
    Check(tail.Git("rev-list", "--count", "HEAD") == "2", "three tail commits become one");
    Check(tail.Git("show", "HEAD:file.txt") == "original", "net empty change retained");
    Check(tail.Git("show", "-s", "--format=%an <%ae>", "HEAD") == "Original Author <author@example.invalid>", "oldest author retained");
    Check(tail.Git("rev-parse", "keep-this-tip") == last, "other branch is not rewritten by updateRefs config");
    Check(tail.Git("rev-parse", operation.RecoveryRef) == last, "backup is not rewritten by updateRefs config");
    Check(tail.Git("show", "-s", "--format=%B", "HEAD") == "Empty combined change\n\n# literal message", "literal message retained");
}
Console.WriteLine("PASS: three tail commits, empty changes, authorship and other references");

using (var main = new TestRepository())
{
    main.Commit("root", "root.txt", "root");
    var first = main.Commit("B", "b.txt", "B");
    var last = main.Commit("C", "c.txt", "C");
    var linked = main.Path + "-linked";
    main.Git("worktree", "add", "-b", "linked", linked);
    try
    {
        var operation = new SquashCommits(linked);
        Check(await operation.PrepareAsync([last, first]), "prepare linked worktree");
        Check(await operation.SquashAsync("BC"), "squash linked worktree");
        Check(main.Git("-C", linked, "log", "--format=%s", "--reverse") == "root\nBC", "linked history squashed");
        Check(main.Git("rev-parse", "main") == last, "main worktree branch remains unchanged");
    }
    finally
    {
        main.Git("worktree", "remove", linked);
    }
}
Console.WriteLine("PASS: linked worktree");

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class TestRepository : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sourcegit-squash-tests-" + Guid.NewGuid().ToString("N"));

    public TestRepository()
    {
        Directory.CreateDirectory(Path);
        Git("init", "-b", "main");
        Git("config", "user.name", "Squash Test");
        Git("config", "user.email", "squash@example.invalid");
        Git("config", "commit.gpgsign", "false");
    }

    public string Commit(string message, string file, string content)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, file), content);
        Git("add", "--", file);
        Git("commit", "-m", message);
        return Git("rev-parse", "HEAD");
    }

    public string Git(params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Keep tests independent of the developer's aliases, signing and hooks.
        info.Environment["GIT_CONFIG_GLOBAL"] = System.IO.Path.Combine(Path, "no-global-config");
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output.Trim();
    }

    public void Dispose() => Directory.Delete(Path, true);
}
