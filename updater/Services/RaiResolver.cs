using IptvUpdater.Models;

namespace IptvUpdater.Services;

public class RaiResolver(RaiConfig config)
{
    private const string RelinkerBase =
        "https://mediapolis.rai.it/relinker/relinkerServlet.htm";

    public async Task<List<Channel>> ResolveAsync()
    {
        Console.WriteLine("\n[RAI] Rigenerazione token...");
        var tasks = config.Channels.Select(ResolveOneAsync);
        var results = await Task.WhenAll(tasks);
        return [.. results.Where(c => c is not null)];
    }

    private async Task<Channel?> ResolveOneAsync(RaiChannel rai)
    {
        var relinkerUrl =
            $"{RelinkerBase}?cont={rai.Cont}&output=7&forceUserAgent=raiplayappletv";
        try
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            client.DefaultRequestHeaders
                  .TryAddWithoutValidation("User-Agent", config.UserAgent);

            var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, relinkerUrl));

            string? streamUrl = (int)response.StatusCode is 301 or 302 or 307 or 308
                ? response.Headers.Location?.ToString()
                : null;

            if (streamUrl is not null && streamUrl.Contains(".m3u8"))
            {
                Console.WriteLine($"  \u2713 {rai.Name} \u2192 token ok");
                return new Channel
                {
                    Name        = rai.Name,
                    Group       = rai.Group,
                    ChNo        = rai.ChNo,
                    TvgId       = rai.TvgId,
                    Url         = relinkerUrl,
                    ResolvedUrl = streamUrl,
                    Status      = ChannelStatus.Regenerated,
                };
            }

            Console.WriteLine(
                $"  \u2717 {rai.Name} \u2192 nessun redirect .m3u8 (HTTP {(int)response.StatusCode})");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u2717 {rai.Name} \u2192 {ex.Message}");
            return null;
        }
    }
}
