using IptvUpdater.Models;
using Octokit;

namespace IptvUpdater.Services;

public class GitHubPublisher(GitHubConfig config)
{
    public async Task<bool> PublishAsync(string content)
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("IptvUpdater"));
            client.Credentials = new Credentials(config.Token);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var lineCount = content.Split('\n')
                .Count(l => l.StartsWith("http", StringComparison.Ordinal));
            var commitMsg = $"Auto-update {timestamp} — {lineCount} canali";

            string? existingSha = null;
            try
            {
                var existing = await client.Repository.Content
                    .GetAllContents(config.Owner, config.Repo, config.FilePath);
                existingSha = existing.FirstOrDefault()?.Sha;
            }
            catch (NotFoundException) { }

            if (existingSha is not null)
            {
                await client.Repository.Content.UpdateFile(
                    config.Owner, config.Repo, config.FilePath,
                    new UpdateFileRequest(commitMsg, content, existingSha, config.Branch));
            }
            else
            {
                await client.Repository.Content.CreateFile(
                    config.Owner, config.Repo, config.FilePath,
                    new CreateFileRequest(commitMsg, content, config.Branch));
            }

            Console.WriteLine($"  ✓ {config.FilePath} aggiornato ({lineCount} canali)");
            Console.WriteLine($"\nURL SS IPTV:");
            Console.WriteLine(
                $"https://raw.githubusercontent.com/{config.Owner}/{config.Repo}/{config.Branch}/{config.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Errore GitHub: {ex.Message}");
            return false;
        }
    }
}
