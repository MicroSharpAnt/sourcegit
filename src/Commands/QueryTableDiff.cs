using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class QueryTableDiff
    {
        public static async Task<Models.TableDiffDocument> LoadAsync(string repo, Models.DiffOption option)
        {
            var old = LoadSideAsync(repo, option.TableOld);
            var cur = LoadSideAsync(repo, option.TableNew);
            await Task.WhenAll(old, cur).ConfigureAwait(false);
            return new Models.TableDiffDocument(await old.ConfigureAwait(false), await cur.ConfigureAwait(false));
        }

        private static Task<Models.TablePreview> LoadSideAsync(string repo, Models.TableFileSource source) =>
            source.IsMissing ? Task.FromResult(new Models.TablePreview()) : QueryTablePreview.LoadAsync(repo, source.Path, source.Revision);
    }
}
