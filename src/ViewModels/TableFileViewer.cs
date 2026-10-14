using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TableFileViewer(string repo, string file, string revision) : ObservableObject
    {
        public string File => file;
        public string Source => revision == null ? "Working tree" : revision.Length == 0 ? "Index" : revision;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }
        public Models.TablePreview Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public async Task LoadAsync()
        {
            Content = await Commands.QueryTablePreview.LoadAsync(repo, file, revision);
            IsLoading = false;
        }

        private bool _isLoading = true;
        private Models.TablePreview _content;
    }
}
