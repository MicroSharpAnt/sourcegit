# Squash integration tests

Run `dotnet run --project tests/SourceGit.IntegrationTests -p:DisableAOT=true` from the repository root with .NET 10, Git and `ssh-keygen` installed. The signing-recovery test creates an ephemeral key inside its disposable repository.

The runner exercises the public squash command against disposable repositories, using the app's real rebase editor entry points. It throws and exits nonzero on a failed assertion. It does not rewrite the source repository's history.

The UI is checked separately: select consecutive commits in history, open **Squash Commits**, edit the message and confirm. For an invalid selection, verify that the explanation appears and history stays unchanged.
