namespace SourceGit.ViewModels
{
    public class ConflictSource(string role, string name, Models.Commit commit)
    {
        public Models.Commit Commit { get; } = commit;
        public string Title => string.IsNullOrEmpty(name) ? App.Text(role) : $"{App.Text(role)}: {name}";
        public string Hash => string.IsNullOrEmpty(Commit?.SHA) ? string.Empty : "…" + Commit.SHA[System.Math.Max(0, Commit.SHA.Length - 8)..];
        public string UseLabel => App.Text("ConflictSource.Use", $"{Title} {Hash}".TrimEnd());
    }

    public class ConflictSources
    {
        public ConflictSource Mine { get; private init; }
        public ConflictSource Theirs { get; private init; }

        public static ConflictSources Create(InProgressContext context, Models.Commit head, string currentBranch)
        {
            var mine = new ConflictSource(string.IsNullOrEmpty(currentBranch) ? "ConflictSource.CurrentContent" : "ConflictSource.Current", currentBranch, head);
            var theirs = new ConflictSource("ConflictSource.Patch", null, null);
            switch (context)
            {
                case MergeInProgress merge:
                    mine = new(string.IsNullOrEmpty(merge.Current) ? "ConflictSource.CurrentContent" : "ConflictSource.Current", merge.Current, head);
                    theirs = new("ConflictSource.Incoming", FriendlyName(merge.Source), merge.Source);
                    break;
                case RebaseInProgress rebase:
                    // Onto is the original target; HEAD includes commits already replayed.
                    mine = new("ConflictSource.Target", "HEAD", head);
                    theirs = new("ConflictSource.Replaying", rebase.HeadName, rebase.StoppedAt);
                    break;
                case CherryPickInProgress cherryPick:
                    theirs = new("ConflictSource.Picking", FriendlyName(cherryPick.Head), cherryPick.Head);
                    break;
                case RevertInProgress revert:
                    // The commit identifies the operation being undone, not the right-side blob.
                    theirs = new("ConflictSource.Reverting", null, revert.Head);
                    break;
                default:
                    // Applying a stash/patch can conflict with local changes not present in HEAD.
                    mine = new("ConflictSource.CurrentContent", null, null);
                    break;
            }
            return new() { Mine = mine, Theirs = theirs };
        }

        private static string FriendlyName(Models.Commit commit) => commit?.Decorators.Find(x =>
            x.Type is Models.DecoratorType.LocalBranchHead or Models.DecoratorType.RemoteBranchHead or Models.DecoratorType.Tag)?.Name;
    }
}
