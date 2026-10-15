using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGit.Models
{
    public enum TableChangeKind { Unchanged, Added, Deleted, Modified }

    public class TableDiffRow
    {
        public int OldLine { get; init; }
        public int NewLine { get; init; }
        public string[] Old { get; init; }
        public string[] New { get; init; }
        public TableChangeKind[] Cells { get; init; }
        public TableChangeKind Kind { get; init; }
    }

    public class TableDiff
    {
        public List<TableDiffRow> Rows { get; } = [];
        public int ColumnCount { get; private set; }
        public bool AlignmentLimited { get; private set; }
        public bool AlignmentAmbiguous { get; private set; }
        public bool KeyIsAmbiguous { get; private set; }

        public static TableDiff Compare(TableSheet oldSheet, TableSheet newSheet, int keyColumn = -1)
        {
            var result = new TableDiff { ColumnCount = Math.Max(oldSheet?.ColumnCount ?? 0, newSheet?.ColumnCount ?? 0) };
            var oldRows = oldSheet?.Rows ?? [];
            var newRows = newSheet?.Rows ?? [];
            if (keyColumn >= 0)
            {
                var oldMap = UniquePositions(oldRows.Select(x => Value(x, keyColumn)).ToArray());
                var newMap = UniquePositions(newRows.Select(x => Value(x, keyColumn)).ToArray());
                result.KeyIsAmbiguous = oldMap.ContainsKey(string.Empty) || newMap.ContainsKey(string.Empty) ||
                    oldMap.ContainsValue(-1) || newMap.ContainsValue(-1);
                if (!result.KeyIsAmbiguous)
                {
                    for (var j = 0; j < newRows.Count; j++)
                        result.Add(oldRows, newRows, oldMap.TryGetValue(Value(newRows[j], keyColumn), out var i) ? i : -1, j);
                    for (var i = 0; i < oldRows.Count; i++)
                    {
                        if (!newMap.ContainsKey(Value(oldRows[i], keyColumn)))
                            result.Add(oldRows, newRows, i, -1);
                    }
                    return result;
                }
            }
            var oldKeys = oldRows.Select(Identity).ToArray();
            var newKeys = newRows.Select(Identity).ToArray();
            var oldUnique = UniquePositions(oldKeys);
            var newUnique = UniquePositions(newKeys);
            var candidates = new List<(int Old, int New)>();
            for (var i = 0; i < oldKeys.Length; i++)
            {
                if (oldUnique[oldKeys[i]] >= 0 && newUnique.TryGetValue(oldKeys[i], out var j) && j >= 0)
                    candidates.Add((i, j));
            }

            // Patience anchors: unique unchanged rows in a longest increasing sequence.
            var tails = new List<int>();
            var previous = new int[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                var low = 0;
                var high = tails.Count;
                while (low < high)
                {
                    var mid = (low + high) / 2;
                    if (candidates[tails[mid]].New < candidates[i].New)
                        low = mid + 1;
                    else
                        high = mid;
                }
                previous[i] = low == 0 ? -1 : tails[low - 1];
                if (low == tails.Count)
                    tails.Add(i);
                else
                    tails[low] = i;
            }
            var anchors = new List<(int Old, int New)>();
            for (var i = tails.Count == 0 ? -1 : tails[^1]; i >= 0; i = previous[i])
                anchors.Add(candidates[i]);
            anchors.Reverse();
            anchors.Add((oldRows.Count, newRows.Count));

            var oldStart = 0;
            var newStart = 0;
            var budget = 1000000;
            foreach (var anchor in anchors)
            {
                result.AlignBlock(oldRows, newRows, oldStart, anchor.Old, newStart, anchor.New, ref budget);
                if (anchor.Old < oldRows.Count)
                    result.Add(oldRows, newRows, anchor.Old, anchor.New);
                oldStart = anchor.Old + 1;
                newStart = anchor.New + 1;
            }
            return result;
        }

        private void AlignBlock(List<string[]> oldRows, List<string[]> newRows, int oldStart, int oldEnd, int newStart, int newEnd, ref int budget)
        {
            var m = oldEnd - oldStart;
            var n = newEnd - newStart;
            var work = (long)m * n;
            if (work == 0 || work > 250000 || work > budget)
            {
                AlignmentLimited |= work > 0;
                for (var i = oldStart; i < oldEnd; i++)
                    Add(oldRows, newRows, i, -1);
                for (var j = newStart; j < newEnd; j++)
                    Add(oldRows, newRows, -1, j);
                return;
            }
            budget -= (int)work;
            var costs = new int[m + 1, n + 1];
            var steps = new byte[m + 1, n + 1];
            var ambiguous = new bool[m + 1, n + 1];
            for (var i = 1; i <= m; i++)
                costs[i, 0] = i * 200;
            for (var j = 1; j <= n; j++)
                costs[0, j] = j * 200;
            for (var i = 1; i <= m; i++)
            {
                for (var j = 1; j <= n; j++)
                {
                    var pair = costs[i - 1, j - 1] + PairCost(oldRows[oldStart + i - 1], newRows[newStart + j - 1]);
                    var delete = costs[i - 1, j] + 200;
                    var insert = costs[i, j - 1] + 200;
                    costs[i, j] = Math.Min(pair, Math.Min(delete, insert));
                    var best = costs[i, j];
                    ambiguous[i, j] = (pair == best ? 1 : 0) + (delete == best ? 1 : 0) + (insert == best ? 1 : 0) > 1 ||
                        (pair == best && ambiguous[i - 1, j - 1]) ||
                        (delete == best && ambiguous[i - 1, j]) || (insert == best && ambiguous[i, j - 1]);
                    steps[i, j] = pair <= delete && pair <= insert ? (byte)0 : delete <= insert ? (byte)1 : (byte)2;
                }
            }
            AlignmentAmbiguous |= ambiguous[m, n];
            var pairs = new List<(int Old, int New)>();
            var x = m;
            var y = n;
            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && steps[x, y] == 0)
                    pairs.Add((oldStart + --x, newStart + --y));
                else if (x > 0 && (y == 0 || steps[x, y] == 1))
                    pairs.Add((oldStart + --x, -1));
                else
                    pairs.Add((-1, newStart + --y));
            }
            pairs.Reverse();
            foreach (var pair in pairs)
                Add(oldRows, newRows, pair.Old, pair.New);
        }

        private void Add(List<string[]> oldRows, List<string[]> newRows, int oldIndex, int newIndex)
        {
            var old = oldIndex >= 0 ? oldRows[oldIndex] : null;
            var cur = newIndex >= 0 ? newRows[newIndex] : null;
            var cells = new TableChangeKind[ColumnCount];
            var kind = old == null ? TableChangeKind.Added : cur == null ? TableChangeKind.Deleted : TableChangeKind.Unchanged;
            for (var i = 0; i < cells.Length; i++)
            {
                var before = Value(old, i);
                var after = Value(cur, i);
                cells[i] = old == null ? TableChangeKind.Added : cur == null ? TableChangeKind.Deleted :
                    before == after ? TableChangeKind.Unchanged : before.Length == 0 ? TableChangeKind.Added :
                    after.Length == 0 ? TableChangeKind.Deleted : TableChangeKind.Modified;
                if (kind == TableChangeKind.Unchanged && cells[i] != TableChangeKind.Unchanged)
                    kind = TableChangeKind.Modified;
            }
            Rows.Add(new TableDiffRow { OldLine = oldIndex + 1, NewLine = newIndex + 1, Old = old, New = cur, Cells = cells, Kind = kind });
        }

        public static string Value(string[] row, int column) => row != null && column < row.Length ? row[column] ?? string.Empty : string.Empty;

        private static int PairCost(string[] old, string[] cur)
        {
            var equal = true;
            var shared = 0;
            var count = Math.Max(old.Length, cur.Length);
            for (var i = 0; i < Math.Max(old.Length, cur.Length); i++)
            {
                var a = Value(old, i);
                var b = Value(cur, i);
                equal &= a == b;
                if (a.Length > 0 && a == b)
                    shared++;
            }
            return equal ? 0 : 300 - 250 * shared / Math.Max(1, count);
        }

        private static string Identity(string[] row)
        {
            var length = row.Length;
            while (length > 0 && string.IsNullOrEmpty(row[length - 1]))
                length--;
            var result = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var value = row[i] ?? string.Empty;
                result.Append(value.Length).Append(':').Append(value);
            }
            return result.ToString();
        }

        private static Dictionary<string, int> UniquePositions(string[] keys)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < keys.Length; i++)
            {
                if (!result.TryAdd(keys[i], i))
                    result[keys[i]] = -1;
            }
            return result;
        }
    }
}
