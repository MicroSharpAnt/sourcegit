using System.Diagnostics;
using System.IO;

namespace SourceGit.Native
{
    public static class MacOSFileManager
    {
        public static readonly string[] Names = ["Finder", "QSpace", "QSpace Pro"];

        public static ProcessStartInfo CreateStartInfo(string path, int selection)
        {
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
                return null;

            if (selection < 0 || selection >= Names.Length)
                selection = 0;

            var start = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            start.ArgumentList.Add("-a");
            start.ArgumentList.Add(Names[selection]);
            // QSpace handles document-open events by revealing the file itself.
            // macOS's -R flag specifically requests Finder, so use it only for Finder.
            if (selection == 0 && !isDirectory)
                start.ArgumentList.Add("-R");
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(Path.GetFullPath(path));
            return start;
        }
    }
}
