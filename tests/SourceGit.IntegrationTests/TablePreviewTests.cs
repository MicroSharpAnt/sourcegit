using System.Text;
using System.IO.Compression;
using SourceGit.Models;

static class TablePreviewTests
{
    public static async Task RunAsync()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("id,note,empty\r\n001,\"hello, \"\"world\"\"\nnext\",\r\n"));
        var preview = TablePreview.Read("sample.csv", input);
        Check(preview.Sheets.Count == 1, "CSV has one sheet: " + preview.Error);
        var rows = preview.Sheets[0].Rows;
        Check(rows.Count == 2, "CSV embedded newline is not another row");
        Check(rows[1][0] == "001" && rows[1][1] == "hello, \"world\"\nnext" && rows[1][2].Length == 0, "CSV preserves text, escaped quotes and empty cells");
        Check(preview.RawText.Content.Contains("\"\"world\"\""), "CSV raw text remains unchanged");
        Console.WriteLine("PASS: CSV table and raw text");
        using var workbook = CreateWorkbook();
        var excel = TablePreview.Read("sample.xlsx", workbook);
        Check(excel.Error == null && excel.Sheets.Count == 2, "XLSX worksheets: " + excel.Error);
        Check(excel.Sheets[0].Name == "Data" && excel.Sheets[1].Name == "Other", "worksheet names and order");
        Check(excel.Sheets[0].Rows[0][0] == "名称" && excel.Sheets[0].Rows[0][2] == "42", "XLSX sparse cells keep column positions");
        Check(excel.Sheets[1].Rows[0][0] == "3", "cached formula value is displayed");
        Console.WriteLine("PASS: XLSX sheets, sparse cells and stored formula values");
        using var repo = new TestRepository();
        var first = repo.Commit("table", "data.csv", "id,value\n1,committed\n");
        File.WriteAllText(Path.Combine(repo.Path, "data.csv"), "id,value\n1,staged\n");
        repo.Git("add", "data.csv");
        File.WriteAllText(Path.Combine(repo.Path, "data.csv"), "id,value\n1,working\n");
        var committed = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, "data.csv", first);
        var staged = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, "data.csv", "");
        var working = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, "data.csv", null);
        Check(committed.Sheets[0].Rows[1][1] == "committed", "history does not read disk");
        Check(staged.Sheets[0].Rows[1][1] == "staged", "index does not read disk");
        Check(working.Sheets[0].Rows[1][1] == "working", "working tree reads disk");
        var missing = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, "missing.csv", first);
        Check(!string.IsNullOrEmpty(missing.Error), "missing Git blob reports error, not empty table");
        foreach (var unstaged in new[] { true, false })
        {
            var option = new DiffOption(new Change { Path = "data.csv", Index = ChangeState.Modified, WorkTree = ChangeState.Modified }, unstaged);
            var loaded = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, option.PreviewPath, option.PreviewRevision);
            Check(loaded.Sheets[0].Rows[1][1] == (unstaged ? "working" : "staged"), "diff preview uses the selected stage");
        }
        repo.Git("add", "data.csv");
        repo.Git("commit", "-m", "working");
        repo.Git("mv", "data.csv", "renamed.csv");
        File.Delete(Path.Combine(repo.Path, "renamed.csv"));
        var renamedDeletion = new DiffOption(new Change { Path = "renamed.csv", OriginalPath = "data.csv", Index = ChangeState.Renamed, WorkTree = ChangeState.Deleted }, true);
        var indexBeforeDeletion = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, renamedDeletion.PreviewPath, renamedDeletion.PreviewRevision);
        Check(indexBeforeDeletion.Error == null && indexBeforeDeletion.Sheets[0].Rows[1][1] == "working", "unstaged deletion after staged rename reads index new path");
        repo.Git("checkout", "--", "renamed.csv");
        repo.Git("commit", "-m", "rename");
        var renamed = new FileVersion { SHA = repo.Git("rev-parse", "HEAD"), HasParent = true, Change = new Change { Path = "renamed.csv", OriginalPath = "data.csv", Index = ChangeState.Renamed } };
        var renamedOption = new DiffOption(renamed);
        var renamedPreview = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, renamedOption.PreviewPath, renamedOption.PreviewRevision);
        Check(renamedPreview.Sheets[0].Rows[1][1] == "working", "renamed history uses raw revision and new path");
        repo.Git("rm", "renamed.csv");
        var deletedOption = new DiffOption(new Change { Path = "renamed.csv", Index = ChangeState.Deleted }, false);
        var deletedPreview = await SourceGit.Commands.QueryTablePreview.LoadAsync(repo.Path, deletedOption.PreviewPath, deletedOption.PreviewRevision);
        Check(deletedPreview.Sheets[0].Rows[1][1] == "working", "deleted staged file previews HEAD");
        using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("not a workbook"));
        Check(TablePreview.Read("broken.xlsx", invalid).Error != null, "malformed workbook reports an error");
        using var oversized = new MemoryStream(new byte[TablePreview.MaxFileSize + 1]);
        Check(TablePreview.Read("large.csv", oversized).Error != null, "oversized input rejected");
        using var manyRows = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a,b\n", TablePreview.MaxRows + 1))));
        var bounded = TablePreview.Read("large.csv", manyRows);
        Check(bounded.IsTruncated && bounded.Sheets[0].Rows.Count == TablePreview.MaxRows, "row limit explicitly marked");
        using var emptyCsv = new MemoryStream();
        Check(TablePreview.Read("empty.csv", emptyCsv).Error == null, "empty CSV is a valid empty table");
        Console.WriteLine("PASS: source routing, rename, deletion, malformed and bounded previews");
        Console.WriteLine("PASS: committed, staged, working-tree and missing sources");
    }

    private static MemoryStream CreateWorkbook()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(xml);
            }
            Entry("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/></Types>
                """);
            Entry("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Entry("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Data" sheetId="1" r:id="rId1"/><sheet name="Other" sheetId="2" r:id="rId2"/></sheets></workbook>
                """);
            Entry("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/></Relationships>
                """);
            Entry("xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>名称</t></is></c><c r="C1"><v>42</v></c></row></sheetData></worksheet>
                """);
            Entry("xl/worksheets/sheet2.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1"><f>1+2</f><v>3</v></c></row></sheetData></worksheet>
                """);
        }
        stream.Position = 0;
        return stream;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
