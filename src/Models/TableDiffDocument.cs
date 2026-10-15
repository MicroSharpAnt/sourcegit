using System.Collections.Generic;
using System.Linq;

namespace SourceGit.Models
{
    public record TableFileSource(string Path, string Revision, bool IsMissing = false);

    public record TableSheetPair(string Name, TableSheet Old, TableSheet New)
    {
        public TableChangeKind Kind => Old == null ? TableChangeKind.Added : New == null ? TableChangeKind.Deleted : TableChangeKind.Unchanged;
        public string Label => Kind == TableChangeKind.Added ? $"[+] {Name}" : Kind == TableChangeKind.Deleted ? $"[-] {Name}" : Name;
    }

    public class TableDiffDocument
    {
        public List<TableSheetPair> Sheets { get; } = [];
        public string Error { get; }
        public bool IsTruncated { get; }

        public TableDiffDocument(TablePreview old, TablePreview cur)
        {
            if (old.Error != null || cur.Error != null)
            {
                Error = $"Old: {old.Error ?? "OK"}\nNew: {cur.Error ?? "OK"}";
                return;
            }
            IsTruncated = old.IsTruncated || cur.IsTruncated || old.Sheets.Any(x => x.IsTruncated) || cur.Sheets.Any(x => x.IsTruncated);
            if (IsTruncated)
                return;

            // CSV worksheet names are filenames, and must not turn a rename into two sheets.
            if ((old.RawText != null || cur.RawText != null) && old.Sheets.Count <= 1 && cur.Sheets.Count <= 1)
            {
                var before = old.Sheets.FirstOrDefault();
                var after = cur.Sheets.FirstOrDefault();
                Sheets.Add(new TableSheetPair(after?.Name ?? before?.Name ?? "CSV", before, after));
                return;
            }
            foreach (var sheet in old.Sheets)
                Sheets.Add(new TableSheetPair(sheet.Name, sheet, cur.Sheets.Find(x => x.Name == sheet.Name)));
            foreach (var sheet in cur.Sheets)
            {
                if (!old.Sheets.Exists(x => x.Name == sheet.Name))
                    Sheets.Add(new TableSheetPair(sheet.Name, null, sheet));
            }
        }
    }
}
