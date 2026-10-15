using SourceGit.Models;

static class TableDiffTests
{
    public static async Task RunAsync()
    {
        var oldSheet = Sheet("Data", ["id", "value"], ["1", "one"], ["2", "two"], ["3", "three"]);
        var newSheet = Sheet("Data", ["id", "value"], ["0", "zero"], ["1", "ONE"], ["3", "three"]);
        var diff = TableDiff.Compare(oldSheet, newSheet);
        Check(diff.Rows.Count(x => x.Kind == TableChangeKind.Added) == 1, "inserted row is added");
        Check(diff.Rows.Count(x => x.Kind == TableChangeKind.Deleted) == 1, "removed row is deleted");
        var edit = diff.Rows.Single(x => x.Kind == TableChangeKind.Modified);
        Check(edit.OldLine == 2 && edit.NewLine == 3, "modified row keeps original coordinates");
        Check(edit.Cells[0] == TableChangeKind.Unchanged && edit.Cells[1] == TableChangeKind.Modified, "only edited cell highlighted");
        Console.WriteLine("PASS: automatic row alignment and cell highlights");
        var repeatedFields = TableDiff.Compare(
            Sheet("Data", ["1", "Item 1", "10", "same", "extra"], ["2", "Item 2", "20", "same", "extra"]),
            Sheet("Data", ["1", "Item 1", "999", "same", "extra"]));
        Check(repeatedFields.Rows.Single(x => x.Kind == TableChangeKind.Modified).OldLine == 1, "automatic alignment prefers more matching cells despite common fields");
        var keyed = TableDiff.Compare(Sheet("Data", ["1", "a"], ["2", "b"]), Sheet("Data", ["2", "B"], ["1", "a"]), 0);
        Check(keyed.Rows.Count == 2 && keyed.Rows[0].OldLine == 2 && keyed.Rows[0].NewLine == 1, "key aligns reordered rows");
        Check(keyed.Rows[0].Kind == TableChangeKind.Modified && keyed.Rows[1].Kind == TableChangeKind.Unchanged, "key compares same entity");
        var duplicates = TableDiff.Compare(Sheet("Data", ["1", "a"], ["1", "b"]), Sheet("Data", ["1", "a"], ["1", "B"]), 0);
        Check(duplicates.KeyIsAmbiguous && duplicates.Rows.Count == 2, "duplicate keys warn without dropping data");
        var blanks = TableDiff.Compare(Sheet("Data", ["", "a"]), Sheet("Data", ["", "b"]), 0);
        Check(blanks.KeyIsAmbiguous, "blank keys warn");
        Console.WriteLine("PASS: keyed reordering and ambiguous keys");
        var ambiguous = TableDiff.Compare(Sheet("Data", ["a"], ["a"]), Sheet("Data", ["a"]));
        Check(ambiguous.AlignmentAmbiguous, "equally valid automatic alignments are disclosed");
        var before = new TablePreview();
        before.Sheets.Add(Sheet("Removed"));
        before.Sheets.Add(Sheet("Shared", ["a"]));
        var after = new TablePreview();
        after.Sheets.Add(Sheet("Shared", ["A"]));
        after.Sheets.Add(Sheet("Added"));
        var document = new TableDiffDocument(before, after);
        Check(document.Sheets.Count == 3, "worksheet union retains empty added/deleted sheets");
        Check(document.Sheets.Single(x => x.Name == "Removed").Kind == TableChangeKind.Deleted, "deleted sheet status");
        Check(document.Sheets.Single(x => x.Name == "Added").Kind == TableChangeKind.Added, "added sheet status");
        using var repo = new TestRepository();
        var first = repo.Commit("table", "old.csv", "id,value\n1,old\n");
        repo.Git("mv", "old.csv", "new.csv");
        File.WriteAllText(Path.Combine(repo.Path, "new.csv"), "id,value\n1,index\n");
        repo.Git("add", "new.csv");
        File.WriteAllText(Path.Combine(repo.Path, "new.csv"), "id,value\n1,disk\n");
        var rename = new Change { Path = "new.csv", OriginalPath = "old.csv", Index = ChangeState.Renamed, WorkTree = ChangeState.Modified };
        var staged = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(rename, false));
        Check(staged.Error == null && staged.Sheets.Count == 1, "renamed CSV compares as one table");
        Check(staged.Sheets[0].Old.Rows[1][1] == "old" && staged.Sheets[0].New.Rows[1][1] == "index", "staged rename uses HEAD old path and index new path");
        var unstaged = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(rename, true));
        Check(unstaged.Sheets[0].Old.Rows[1][1] == "index" && unstaged.Sheets[0].New.Rows[1][1] == "disk", "unstaged rename uses index new path and disk");
        var added = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(new FileVersion { SHA = first, Change = new Change { Path = "old.csv", Index = ChangeState.Added } }));
        Check(added.Error == null && added.Sheets[0].Old == null, "root addition has intentionally absent old side");
        var missing = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(first, first, new Change { Path = "missing.csv", Index = ChangeState.Modified }));
        Check(missing.Error != null, "unexpected missing blob is an error");
        File.Delete(Path.Combine(repo.Path, "new.csv"));
        rename.WorkTree = ChangeState.Deleted;
        var removed = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(rename, true));
        Check(removed.Error == null && removed.Sheets[0].Old.Rows[1][1] == "index" && removed.Sheets[0].New == null, "unstaged deletion after rename uses index new path");
        repo.Git("checkout", "--", "new.csv");
        repo.Git("commit", "-m", "rename");
        var second = repo.Git("rev-parse", "HEAD");
        var history = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption(new FileVersion { SHA = first, Change = new Change { Path = "old.csv", Index = ChangeState.Added } }, new FileVersion { SHA = second, HasParent = true, Change = new Change { Path = "new.csv", OriginalPath = "old.csv", Index = ChangeState.Renamed } }));
        Check(history.Error == null && history.Sheets.Count == 1 && history.Sheets[0].Old.Rows[1][1] == "old", "history comparison reads original filename");
        File.WriteAllText(Path.Combine(repo.Path, "new.csv"), "id,value\n1,disk\n");
        var reverse = await SourceGit.Commands.QueryTableDiff.LoadAsync(repo.Path, new DiffOption("", second, new Change { Path = "new.csv", Index = ChangeState.Modified }));
        Check(reverse.Sheets[0].Old.Rows[1][1] == "disk" && reverse.Sheets[0].New.Rows[1][1] == "index", "reverse working tree comparison retains side orientation");
        var cellValues = TableDiff.Compare(Sheet("Data", ["id", "", "remove", "old"]), Sheet("Data", ["id", "new", "", "NEW"]), 0);
        Check(cellValues.Rows[0].Cells.SequenceEqual(new[] { TableChangeKind.Unchanged, TableChangeKind.Added, TableChangeKind.Deleted, TableChangeKind.Modified }), "empty-cell additions and deletions distinguished");
        var largeOld = Sheet("Data", Enumerable.Range(0, 600).Select(i => new[] { "old" + i }).ToArray());
        var largeNew = Sheet("Data", Enumerable.Range(0, 600).Select(i => new[] { "new" + i }).ToArray());
        var bounded = TableDiff.Compare(largeOld, largeNew);
        Check(bounded.AlignmentLimited && bounded.Rows.Count == 1200, "large unaligned blocks explicit, no rows dropped");
        before.Sheets[0].IsTruncated = true;
        var incomplete = new TableDiffDocument(before, after);
        Check(incomplete.IsTruncated && incomplete.Sheets.Count == 0, "truncated preview cannot claim complete diff");
        Console.WriteLine("PASS: worksheets, Git sources, cell kinds and bounded comparisons");
    }

    private static TableSheet Sheet(string name, params string[][] rows)
    {
        var sheet = new TableSheet { Name = name, ColumnCount = rows.Length == 0 ? 0 : rows.Max(x => x.Length) };
        sheet.Rows.AddRange(rows);
        return sheet;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
