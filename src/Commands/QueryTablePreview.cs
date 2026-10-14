using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class QueryTablePreview
    {
        // null selects the working tree; empty selects the index; otherwise a Git revision.
        public static Task<Models.TablePreview> LoadAsync(string repo, string file, string revision) => Task.Run(async () =>
        {
            try
            {
                using var content = new MemoryStream();
                if (revision == null)
                {
                    await using var input = File.OpenRead(Path.Combine(repo, file));
                    await CopyBoundedAsync(input, content).ConfigureAwait(false);
                }
                else
                {
                    var start = new ProcessStartInfo(Native.OS.GitExecutable)
                    {
                        WorkingDirectory = repo,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    start.ArgumentList.Add("cat-file");
                    start.ArgumentList.Add("blob");
                    start.ArgumentList.Add($"{revision}:{file}");
                    using var process = Process.Start(start)!;
                    var error = process.StandardError.ReadToEndAsync();
                    try
                    {
                        await CopyBoundedAsync(process.StandardOutput.BaseStream, content).ConfigureAwait(false);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        if (!process.HasExited)
                            process.Kill();
                        await process.WaitForExitAsync().ConfigureAwait(false);
                        await error.ConfigureAwait(false);
                        throw;
                    }
                    var message = await error.ConfigureAwait(false);
                    if (process.ExitCode != 0)
                        return Models.TablePreview.Failure(message.Trim());
                }

                content.Position = 0;
                if (content.Length < 4096)
                {
                    using var reader = new StreamReader(content, Encoding.UTF8, true, 1024, true);
                    if (Models.LFSObject.Parse(await reader.ReadToEndAsync().ConfigureAwait(false)) != null)
                        return Models.TablePreview.Failure("This is a Git LFS pointer. The table is not stored in this Git blob.");
                    content.Position = 0;
                }
                return Models.TablePreview.Read(file, content);
            }
            catch (Exception ex)
            {
                return Models.TablePreview.Failure(ex.Message);
            }
        });

        private static async Task CopyBoundedAsync(Stream source, Stream target)
        {
            var buffer = new byte[65536];
            int length;
            while ((length = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                if (target.Length + length > Models.TablePreview.MaxFileSize)
                    throw new InvalidDataException("File exceeds the 32 MiB preview limit.");
                await target.WriteAsync(buffer.AsMemory(0, length)).ConfigureAwait(false);
            }
        }
    }
}
