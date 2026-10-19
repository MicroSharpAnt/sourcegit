using System;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ClearIndexLock : Popup
    {
        public string LockPath => Path.Combine(_repo.GitDir, "index.lock");

        public ClearIndexLock(Repository repo)
        {
            _repo = repo;
        }

        public override bool CanStartDirectly() => false;

        public override Task<bool> Sure()
        {
            if (!_repo.CanClearIndexLock(this))
            {
                _repo.SendNotification(App.Text("Repository.ClearIndexLock.Busy"), true);
                return Task.FromResult(false);
            }

            try
            {
                // Keep the final busy check and deletion together on the UI thread.
                if (!File.Exists(LockPath))
                {
                    _repo.SendNotification(App.Text("Repository.ClearIndexLock.Missing"));
                    return Task.FromResult(true);
                }

                File.Delete(LockPath);
                _repo.SendNotification(App.Text("Repository.ClearIndexLock.Success"));
                _repo.RefreshWorkingCopyChanges();
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                _repo.SendNotification(App.Text("Repository.ClearIndexLock.Failed", e.Message), true);
                return Task.FromResult(false);
            }
        }

        private readonly Repository _repo;
    }
}
