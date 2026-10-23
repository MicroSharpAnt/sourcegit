using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using SourceGit.Models;
using SourceGit.ViewModels;

internal static class ImageRenameTests
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        SourceGit.Native.OS.GitExecutable = "git";
        using var repo = new TestRepository();
        repo.Git("config", "diff.renames", "true");
        using (var bitmap = new SkiaSharp.SKBitmap(2, 2))
        {
            bitmap.Erase(SkiaSharp.SKColors.Red);
            using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(repo.Path, "old.png"), encoded.ToArray());
        }
        repo.Git("add", ".");
        repo.Git("commit", "-m", "image");
        var before = repo.Git("rev-parse", "HEAD");
        repo.Git("mv", "old.png", "new.png");
        var change = new Change { Path = "new.png", OriginalPath = "old.png", Index = ChangeState.Renamed };
        CheckPreview(new DiffOption(change, false), "staged rename");
        repo.Git("commit", "-m", "rename");
        CheckPreview(new DiffOption(before, repo.Git("rev-parse", "HEAD"), change), "committed rename");

        repo.Commit("text", "old.txt", "unchanged text");
        repo.Git("mv", "old.txt", "new.txt");
        var textOption = new DiffOption(new Change { Path = "new.txt", OriginalPath = "old.txt", Index = ChangeState.Renamed }, false);
        if (Load(textOption).Content is not NoOrEOLChange)
            throw new Exception("Text-only rename must retain existing behavior");
        Console.WriteLine("PASS: text-only rename retains existing behavior");

        using (var bitmap = new SkiaSharp.SKBitmap(3, 3))
        {
            bitmap.Erase(SkiaSharp.SKColors.Blue);
            using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(repo.Path, "new.png"), encoded.ToArray());
        }
        var modified = Load(new DiffOption(new Change { Path = "new.png", WorkTree = ChangeState.Modified }, true));
        if (modified.Content is not ImageDiff changed || changed.Old == null || changed.New == null || changed.IsContentUnchanged)
            throw new Exception("Modified image must not show unchanged notice");
        Console.WriteLine("PASS: modified image has previews without unchanged notice");

        void CheckPreview(DiffOption option, string label)
        {
            var context = Load(option);
            if (context.Content is not ImageDiff image || image.Old == null || image.New == null || !image.IsContentUnchanged)
                throw new Exception($"{label}: expected both image previews and unchanged notice, got {context.Content?.GetType().Name}");
            Console.WriteLine($"PASS: {label} shows both images and unchanged notice");
        }

        DiffContext Load(DiffOption option)
        {
            var context = new DiffContext(repo.Path, option);
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (context.Content == null && DateTime.UtcNow < timeout)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            return context;
        }
    }
}
