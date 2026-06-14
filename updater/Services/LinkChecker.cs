using IptvUpdater.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IptvUpdater.Services;

public class LinkChecker(IHttpClientFactory httpFactory, CheckerConfig config)
{
    private static readonly string[] LiveCodes = ["200", "206", "301", "302"];

    public async Task<List<Channel>> CheckAllAsync(List<Channel> channels)
    {
        var semaphore = new SemaphoreSlim(config.MaxParallelism);
        var tasks     = channels.Select(ch => CheckOneAsync(ch, semaphore));
        return [.. await Task.WhenAll(tasks)];
    }

    private async Task<Channel> CheckOneAsync(Channel ch, SemaphoreSlim sem)
    {
        if (ch.Url.Contains("relinkerServlet", StringComparison.OrdinalIgnoreCase))
            return ch with { Status = ChannelStatus.Skipped };

        await sem.WaitAsync();
        try
        {
            var http = httpFactory.CreateClient("checker");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            var req = new HttpRequestMessage(HttpMethod.Head, ch.Url);
            req.Headers.TryAddWithoutValidation("User-Agent", config.UserAgent);

            try
            {
                var resp = await http.SendAsync(req, cts.Token);
                var code = ((int)resp.StatusCode).ToString();

                if (LiveCodes.Contains(code))
                {
                    Console.WriteLine($"  ✓ {ch.Name} [{code}]");
                    return ch with { Status = ChannelStatus.Alive };
                }

                // Fallback GET per server che rifiutano HEAD
                if (code is "405" or "403")
                {
                    var reqGet = new HttpRequestMessage(HttpMethod.Get, ch.Url);
                    reqGet.Headers.TryAddWithoutValidation("User-Agent", config.UserAgent);
                    reqGet.Headers.TryAddWithoutValidation("Range", "bytes=0-0");
                    using var ctsGet = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
                    var respGet = await http.SendAsync(reqGet,
                        HttpCompletionOption.ResponseHeadersRead, ctsGet.Token);
                    var codeGet = ((int)respGet.StatusCode).ToString();
                    if (LiveCodes.Contains(codeGet) || codeGet is "206" or "416")
                    {
                        Console.WriteLine($"  ✓ {ch.Name} [GET {codeGet}]");
                        return ch with { Status = ChannelStatus.Alive };
                    }
                }

                Console.WriteLine($"  ✗ {ch.Name} [{code}]");
                return ch with { Status = ChannelStatus.Dead };
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                Console.WriteLine($"  ✗ {ch.Name} [TIMEOUT]");
                return ch with { Status = ChannelStatus.Dead };
            }
        }
        finally { sem.Release(); }
    }
}
