using System;

namespace SourceGit.Views
{
    public partial class TableFileViewer : ChromelessWindow
    {
        public TableFileViewer()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (DataContext is ViewModels.TableFileViewer vm)
                await vm.LoadAsync();
        }
    }
}
