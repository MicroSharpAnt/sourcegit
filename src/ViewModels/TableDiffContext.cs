using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TableDiffContext(string repo, Models.DiffOption option) : ObservableObject
    {
        public string File => option.Path;
        public string OldSource => Describe(option.TableOld);
        public string NewSource => Describe(option.TableNew);
        public List<Models.TableSheetPair> Sheets { get; private set; } = [];
        public List<string> SheetLabels => Sheets.Select(x => x.Label).ToList();
        public List<string> Keys { get; private set; } = [];
        public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
        public string Error { get => _error; private set => SetProperty(ref _error, value); }
        public string Warning { get => _warning; private set => SetProperty(ref _warning, value); }
        public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
        public Models.TableDiff Result { get => _result; private set => SetProperty(ref _result, value); }
        public List<Models.TableDiffRow> Rows { get => _rows; private set => SetProperty(ref _rows, value); }
        public bool WrapText { get => _wrapText; set => SetProperty(ref _wrapText, value); }
        public bool OnlyChanges
        {
            get => TableViewSettings.Instance.OnlyChanges;
            set
            {
                if (OnlyChanges == value)
                    return;
                TableViewSettings.Instance.OnlyChanges = value;
                if (!_listeningToSettings)
                    RefreshFilter();
            }
        }
        public int SheetIndex
        {
            get => _sheetIndex;
            set
            {
                if (!SetProperty(ref _sheetIndex, value))
                    return;
                _keyIndex = 0;
                var count = value >= 0 && value < Sheets.Count ? Math.Max(Sheets[value].Old?.ColumnCount ?? 0, Sheets[value].New?.ColumnCount ?? 0) : 0;
                Keys = [App.Text("TableDiff.Auto"), .. Enumerable.Range(0, count).Select(ColumnName)];
                OnPropertyChanged(nameof(Keys));
                OnPropertyChanged(nameof(KeyIndex));
                _ = RecompareAsync();
            }
        }
        public int KeyIndex
        {
            get => _keyIndex;
            set { if (SetProperty(ref _keyIndex, value)) _ = RecompareAsync(); }
        }

        public async Task LoadAsync()
        {
            if (!_listeningToSettings)
            {
                TableViewSettings.Instance.PropertyChanged += OnSettingsChanged;
                _listeningToSettings = true;
            }
            OnPropertyChanged(nameof(OnlyChanges));
            _closed = false;
            IsLoading = true;
            _sheetIndex = -1;
            _keyIndex = 0;
            Sheets = [];
            Keys = [];
            Rows = [];
            Result = null;
            Summary = string.Empty;
            Warning = null;
            Error = null;
            OnPropertyChanged(nameof(SheetIndex));
            OnPropertyChanged(nameof(SheetLabels));
            OnPropertyChanged(nameof(Keys));
            OnPropertyChanged(nameof(KeyIndex));
            var generation = ++_generation;
            var document = await Commands.QueryTableDiff.LoadAsync(repo, option);
            if (_closed || generation != _generation)
                return;
            Error = document.IsTruncated ? App.Text("TableDiff.Truncated") : document.Error;
            Sheets = document.Sheets;
            OnPropertyChanged(nameof(SheetLabels));
            if (Sheets.Count > 0)
            {
                _sheetIndex = -1;
                SheetIndex = 0;
            }
            else
            {
                Summary = Error == null ? App.Text("TableDiff.NoSheets") : string.Empty;
                IsLoading = false;
            }
        }

        public void Close()
        {
            TableViewSettings.Instance.PropertyChanged -= OnSettingsChanged;
            _listeningToSettings = false;
            _closed = true;
            _generation++;
        }

        private async Task RecompareAsync()
        {
            var generation = ++_generation;
            if (_closed || SheetIndex < 0 || SheetIndex >= Sheets.Count)
                return;
            var sheet = Sheets[SheetIndex];
            var key = KeyIndex - 1;
            IsLoading = true;
            Rows = [];
            Result = null;
            Summary = string.Empty;
            Warning = null;
            Error = null;
            try
            {
                var result = await Task.Run(() => Models.TableDiff.Compare(sheet.Old, sheet.New, key));
                if (_closed || generation != _generation)
                    return;
                Result = result;
                Warning = string.Join("\n", new[]
                {
                    result.KeyIsAmbiguous ? App.Text("TableDiff.AmbiguousKey") : null,
                    result.AlignmentAmbiguous ? App.Text("TableDiff.AlignmentAmbiguous") : null,
                    result.AlignmentLimited ? App.Text("TableDiff.AlignmentLimited") : null,
                }.Where(x => x != null));
                Summary = App.Text("TableDiff.Summary", result.Rows.Count(x => x.Kind == Models.TableChangeKind.Added),
                    result.Rows.Count(x => x.Kind == Models.TableChangeKind.Deleted), result.Rows.Count(x => x.Kind == Models.TableChangeKind.Modified));
                if (sheet.Kind == Models.TableChangeKind.Added)
                    Summary = App.Text("TableDiff.SheetAdded") + " · " + Summary;
                else if (sheet.Kind == Models.TableChangeKind.Deleted)
                    Summary = App.Text("TableDiff.SheetDeleted") + " · " + Summary;
                FilterRows();
            }
            catch (Exception ex)
            {
                if (!_closed && generation == _generation)
                    Error = ex.Message;
            }
            finally
            {
                if (!_closed && generation == _generation)
                    IsLoading = false;
            }
        }

        private void FilterRows() => Rows = Result == null ? [] : OnlyChanges ? Result.Rows.Where(x => x.Kind != Models.TableChangeKind.Unchanged).ToList() : Result.Rows;

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TableViewSettings.OnlyChanges))
                RefreshFilter();
        }

        private void RefreshFilter()
        {
            OnPropertyChanged(nameof(OnlyChanges));
            FilterRows();
        }

        public static string ColumnName(int index)
        {
            var name = string.Empty;
            for (var n = index + 1; n > 0; n = (n - 1) / 26)
                name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        private static string Describe(Models.TableFileSource source) => $"{(source.IsMissing ? App.Text("TableDiff.Absent") : source.Revision == null ? App.Text("TableDiff.Worktree") : source.Revision.Length == 0 ? App.Text("TableDiff.Index") : source.Revision)} · {source.Path}";

        private bool _closed;
        private bool _isLoading = true;
        private bool _listeningToSettings;
        private bool _wrapText;
        private int _sheetIndex = -1;
        private int _keyIndex;
        private int _generation;
        private string _error;
        private string _warning;
        private string _summary;
        private Models.TableDiff _result;
        private List<Models.TableDiffRow> _rows = [];
    }
}
