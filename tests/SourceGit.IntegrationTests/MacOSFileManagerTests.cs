using System.Text.Json;
using SourceGit.Native;
using SourceGit.ViewModels;

internal static class MacOSFileManagerTests
{
    public static void Run()
    {
        var preferences = JsonSerializer.Deserialize<Preferences>("{}");
        Check(preferences.MacOSFileManager == 0, "existing settings keep Finder");
        preferences.MacOSFileManager = 2;
        var saved = JsonSerializer.Serialize(preferences);
        Check(JsonSerializer.Deserialize<Preferences>(saved).MacOSFileManager == 2, "QSpace Pro selection survives settings serialization");
        preferences.MacOSFileManager = 99;
        Check(preferences.MacOSFileManager == 0, "invalid settings fall back to Finder");
        var folder = Path.Combine(Path.GetTempPath(), "sourcegit-file-manager-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "空 格 \"quoted\" $name.txt");
            File.WriteAllText(file, "test");
            var start = MacOSFileManager.CreateStartInfo(file, 2);
            Check(start.ArgumentList.SequenceEqual(new[] { "-a", "QSpace Pro", "--", file }) && !start.UseShellExecute,
                "QSpace file paths remain one literal argument without Finder reveal flag");
            Check(MacOSFileManager.CreateStartInfo(file, 0).ArgumentList.Contains("-R"), "Finder reveals selected files");
            Check(!MacOSFileManager.CreateStartInfo(folder, 0).ArgumentList.Contains("-R"), "Finder opens directories");
            Check(MacOSFileManager.CreateStartInfo(folder, 1).ArgumentList[1] == "QSpace", "QSpace store edition supported");
            Check(MacOSFileManager.CreateStartInfo(file + ".missing", 2) == null, "missing paths do not launch an app");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
