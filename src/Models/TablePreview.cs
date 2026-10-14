using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

using ExcelDataReader;

namespace SourceGit.Models
{
    public class TableSheet
    {
        public string Name { get; init; }
        public List<string[]> Rows { get; } = [];
        public int ColumnCount { get; set; }
        public bool IsTruncated { get; set; }
    }

    public class TablePreview
    {
        public const int MaxRows = 10000;
        public const int MaxColumns = 256;
        public const int MaxCells = 500000;
        public const long MaxFileSize = 32 * 1024 * 1024;

        public List<TableSheet> Sheets { get; } = [];
        public RevisionTextFile RawText { get; private set; }
        public string Error { get; private set; }
        public bool IsTruncated { get; private set; }

        public static bool Supports(string file) =>
            file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

        public static TablePreview Failure(string message) => new() { Error = message };

        public static TablePreview Read(string file, Stream stream)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var result = new TablePreview();
            try
            {
                if (stream.Length > MaxFileSize)
                    return Failure("File exceeds the 32 MiB preview limit.");

                var csv = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                if (!Supports(file))
                    throw new NotSupportedException("Unsupported table format.");

                if (csv)
                {
                    using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, true);
                    result.RawText = new RevisionTextFile { FileName = file, Content = textReader.ReadToEnd() };
                    if (LFSObject.Parse(result.RawText.Content) != null)
                        throw new InvalidDataException("This is a Git LFS pointer. The table is not stored in this Git blob.");
                }
                else
                {
                    // Bound expanded XML before the reader loads workbook metadata/shared strings.
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                    long expanded = 0;
                    foreach (var entry in archive.Entries)
                    {
                        expanded += entry.Length;
                        if (expanded > 128 * 1024 * 1024)
                            throw new InvalidDataException("Workbook exceeds the 128 MiB expanded preview limit.");
                    }
                }
                stream.Position = 0;
                var config = new ExcelReaderConfiguration
                {
                    LeaveOpen = true,
                    FallbackEncoding = new UTF8Encoding(false, true),
                    AutodetectSeparators = [',', ';', '\t'],
                    TrimWhiteSpace = false,
                    SinglePassMode = true,
                };
                using var reader = csv ? ExcelReaderFactory.CreateCsvReader(stream, config) : ExcelReaderFactory.CreateOpenXmlReader(stream, config);
                var remainingCells = MaxCells;
                do
                {
                    var sheet = new TableSheet { Name = csv ? Path.GetFileName(file) : reader.Name };
                    while (reader.Read())
                    {
                        var columns = Math.Min(reader.FieldCount, MaxColumns);
                        if (sheet.Rows.Count >= MaxRows || remainingCells < Math.Max(columns, 1))
                        {
                            sheet.IsTruncated = true;
                            break;
                        }
                        var cells = new string[columns];
                        for (var i = 0; i < cells.Length; i++)
                        {
                            var value = reader.GetValue(i);
                            cells[i] = value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) :
                                Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                        remainingCells -= Math.Max(columns, 1);
                        sheet.ColumnCount = Math.Max(sheet.ColumnCount, columns);
                        sheet.IsTruncated |= reader.FieldCount > MaxColumns;
                        sheet.Rows.Add(cells);
                    }
                    result.IsTruncated |= sheet.IsTruncated;
                    result.Sheets.Add(sheet);
                    if (result.Sheets.Count >= 64 || remainingCells <= 0)
                    {
                        result.IsTruncated |= reader.NextResult();
                        break;
                    }
                } while (reader.NextResult());
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }
    }
}
