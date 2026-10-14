using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SquashCommits : Popup
    {
        public IReadOnlyList<Models.Commit> Commits => _operation.Commits;

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public SquashCommits(Repository repo, Commands.SquashCommits operation)
        {
            _repo = repo;
            _operation = operation;
            _message = operation.Message;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            var log = _repo.CreateLog("Squash Commits");
            Use(log);
            _operation.Log = log;
            try
            {
                var success = await _operation.SquashAsync(Message);
                if (!string.IsNullOrEmpty(_operation.Error))
                    _repo.SendNotification(App.Text(_operation.Error) + "\n" + _operation.GitError, true);
                if (!string.IsNullOrEmpty(_operation.RecoveryRef))
                    _repo.SendNotification(App.Text("SquashCommits.Recovery", _operation.RecoveryRef));
                return success || _operation.IsRebasing;
            }
            catch (Exception e)
            {
                _repo.SendNotification(e.Message, true);
                return _operation.IsRebasing;
            }
            finally
            {
                log.Complete();
            }
        }

        private readonly Repository _repo;
        private readonly Commands.SquashCommits _operation;
        private string _message;
    }
}
