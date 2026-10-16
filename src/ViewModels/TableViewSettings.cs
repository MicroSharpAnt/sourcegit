using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    // Shared for this application session, without changing saved display preferences.
    public class TableViewSettings : ObservableObject
    {
        public static TableViewSettings Instance { get; } = new();

        public bool OnlyChanges
        {
            get => _onlyChanges;
            set => SetProperty(ref _onlyChanges, value);
        }

        public bool ShowCsvRawText
        {
            get => _showCsvRawText;
            set => SetProperty(ref _showCsvRawText, value);
        }

        private bool _onlyChanges;
        private bool _showCsvRawText;
    }
}
