using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class SquashCommits : Command
    {
        public IReadOnlyList<Models.Commit> Commits => _commits;
        public string Message { get; private set; } = string.Empty;
        public string RecoveryRef { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        public string GitError { get; private set; } = string.Empty;
        public bool IsRebasing => Directory.Exists(Path.Combine(_gitDir, "rebase-merge"));

        public SquashCommits(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> PrepareAsync(IEnumerable<string> selected)
        {
            _commits.Clear();
            _jobs = null;
            Error = string.Empty;
            GitError = string.Empty;
            var hashes = new HashSet<string>(selected);
            if (hashes.Count < 2 || hashes.Any(x => x == null || (x.Length != 40 && x.Length != 64) || !x.All(Uri.IsHexDigit)))
                return Fail("Selection");

            _head = await QueryAsync("rev-parse --verify HEAD");
            _branch = await QueryAsync("symbolic-ref -q HEAD");
            _gitDir = await QueryAsync("rev-parse --absolute-git-dir");
            if (string.IsNullOrEmpty(_head) || string.IsNullOrEmpty(_gitDir))
                return Fail("Git");
            if (string.IsNullOrEmpty(_branch))
                return Fail("Detached");
            if (!await CheckWorkingTreeAsync())
                return false;

            var targets = new List<Models.Commit>();
            foreach (var sha in hashes)
            {
                var commit = await new QuerySingleCommit(WorkingDirectory, sha).GetResultAsync();
                if (commit == null || commit.Parents.Count != 1)
                    return Fail("Selection");
                targets.Add(commit);
            }

            var oldest = targets.Where(x => !hashes.Contains(x.Parents[0])).ToList();
            if (oldest.Count != 1)
                return Fail("Selection");
            var onto = oldest[0].Parents[0];
            var history = await QueryAsync($"rev-list --reverse --parents {onto}..{_head}");
            if (history == null)
                return Fail("Git");

            var jobs = new Models.InteractiveRebaseJobCollection { OrigHead = _head, Onto = onto };
            var messages = new List<string>();
            var previous = onto;
            foreach (var line in history.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || parts[1] != previous)
                    return Fail("Merge");
                var index = jobs.Jobs.Count;
                var message = await QueryAsync($"show --no-show-signature -s --format=%B {parts[0]}");
                if (message == null)
                    return Fail("Git");
                if (index < hashes.Count)
                {
                    if (!hashes.Contains(parts[0]))
                        return Fail("Selection");
                    _commits.Add(targets.Find(x => x.SHA == parts[0]));
                    messages.Add(message);
                }

                jobs.Jobs.Add(new Models.InteractiveRebaseJob
                {
                    SHA = parts[0],
                    Action = index > 0 && index < hashes.Count ? Models.InteractiveRebaseAction.Squash : Models.InteractiveRebaseAction.Pick,
                    Message = message,
                });
                previous = parts[0];
            }

            if (_commits.Count != hashes.Count || previous != _head)
                return Fail("Selection");

            Message = string.Join("\n\n", messages);
            _jobs = jobs;
            return true;
        }

        public async Task<bool> SquashAsync(string message)
        {
            Error = string.Empty;
            GitError = string.Empty;
            if (_jobs == null)
                return Fail("Selection");
            if (string.IsNullOrWhiteSpace(message))
                return Fail("Message");
            if (!await CheckHeadAsync() || !await CheckWorkingTreeAsync())
                return false;

            RecoveryRef = $"refs/heads/sourcegit-backup/squash-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            if (await QueryAsync($"update-ref {RecoveryRef} {_head} {new string('0', _head.Length)}") == null)
                return Fail("Git");

            for (var i = 0; i < _commits.Count; i++)
                _jobs.Jobs[i].Message = message;
            var jobFile = Path.Combine(_gitDir, "sourcegit.interactive_rebase");
            await File.WriteAllTextAsync(jobFile, JsonSerializer.Serialize(_jobs, JsonCodeGen.Default.InteractiveRebaseJobCollection));

            Editor = EditorType.RebaseEditor;
            Args = $"-c rebase.updateRefs=false -c commit.cleanup=verbatim -c core.commentChar=\"^\" -c core.commentString=\"±\" rebase -i --no-autosquash --no-autostash --keep-empty --empty=keep --reapply-cherry-picks --no-fork-point {_jobs.Onto}";
            var result = await RunRebaseStepAsync();
            var handledEmptySquashes = new HashSet<string>();
            while (!result.IsSuccess && IsRebasing)
            {
                // Git's --empty=keep does not cover an amend made empty by squash.
                // Only retry that precise state; conflicts and other failures stay paused.
                var doneFile = Path.Combine(_gitDir, "rebase-merge", "done");
                if (!File.Exists(doneFile))
                    break;
                var done = await File.ReadAllLinesAsync(doneFile);
                var last = done.LastOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (last == null || last.Length < 2 || (last[0] != "s" && last[0] != "squash") ||
                    !_jobs.Jobs.Any(x => x.Action == Models.InteractiveRebaseAction.Squash && x.SHA.StartsWith(last[1], StringComparison.Ordinal)) ||
                    !handledEmptySquashes.Add(last[1]) ||
                    await QueryAsync("diff --quiet --") == null ||
                    await QueryAsync("diff-index --cached --quiet HEAD^ --") == null)
                    break;

                var messageFile = Path.Combine(_gitDir, "sourcegit.squash-message");
                try
                {
                    await File.WriteAllTextAsync(messageFile, message);
                    Args = $"-c commit.cleanup=verbatim commit --amend --allow-empty -F {messageFile.Quoted()}";
                    result = await RunRebaseStepAsync();
                }
                finally
                {
                    if (File.Exists(messageFile))
                        File.Delete(messageFile);
                }

                if (!result.IsSuccess)
                    break;
                Editor = EditorType.RebaseEditor;
                Args = "-c rebase.updateRefs=false -c commit.cleanup=verbatim -c core.commentChar=\"^\" -c core.commentString=\"±\" rebase --continue";
                result = await RunRebaseStepAsync();
            }

            if (!result.IsSuccess)
            {
                GitError = result.StdErr.Trim();
                Error = "SquashCommits.Error.Git";
            }
            if (!IsRebasing && File.Exists(jobFile))
                File.Delete(jobFile);
            return result.IsSuccess;
        }

        private async Task<Result> RunRebaseStepAsync()
        {
            Log?.AppendLine($"$ git {Args}");
            var result = await ReadToEndAsync();
            Log?.AppendLine(result.StdOut);
            Log?.AppendLine(result.StdErr);
            return result;
        }

        private async Task<string> QueryAsync(string args)
        {
            Args = args;
            Editor = EditorType.None;
            var result = await ReadToEndAsync();
            if (result.IsSuccess)
                return result.StdOut.TrimEnd();
            GitError = result.StdErr.Trim();
            return null;
        }

        private async Task<bool> CheckWorkingTreeAsync()
        {
            foreach (var name in new[] { "rebase-merge", "rebase-apply", "sequencer", "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_START" })
            {
                var path = Path.Combine(_gitDir, name);
                if (File.Exists(path) || Directory.Exists(path))
                    return Fail("InProgress");
            }

            var status = await QueryAsync("status --porcelain=v1 --untracked-files=all --ignore-submodules=dirty");
            if (status == null)
                return Fail("Git");
            return status.Length == 0 || Fail("Dirty");
        }

        private async Task<bool> CheckHeadAsync()
        {
            var head = await QueryAsync("rev-parse --verify HEAD");
            var branch = await QueryAsync("symbolic-ref -q HEAD");
            return (head == _head && branch == _branch) || Fail("Changed");
        }

        private bool Fail(string reason)
        {
            Error = $"SquashCommits.Error.{reason}";
            return false;
        }

        private readonly List<Models.Commit> _commits = [];
        private string _head = string.Empty;
        private string _branch = string.Empty;
        private string _gitDir = string.Empty;
        private Models.InteractiveRebaseJobCollection _jobs;
    }
}
