using IptvUpdater.Models;
using System.Diagnostics;

namespace IptvUpdater.Services;

public class EcuadorResolver(IHttpClientFactory httpFactory, EcuadorConfig config)
{
    public async Task<List<Channel>> ResolveAsync()
    {
        Console.WriteLine("\n[Ecuador] Connetto NordVPN...");

        if (!await NordVpnConnectAsync(config.NordVpnCountry))
        {
            Console.WriteLine(
                "  ✗ NordVPN non disponibile — canali Ecuador saltati.\n" +
                "    Esegui: nordvpn login");
            return [];
        }

        try
        {
            await Task.Delay(3000); // attesa handshake VPN
            var country = await GetCurrentCountryAsync();
            Console.WriteLine($"  IP corrente: {country.Trim()}");

            var results = new List<Channel>();
            foreach (var ch in config.Channels)
            {
                var resolved = await ResolveOneAsync(ch);
                if (resolved is not null) results.Add(resolved);
            }
            return results;
        }
        finally
        {
            Console.WriteLine("  Disconnetto NordVPN...");
            await NordVpnDisconnectAsync();
        }
    }

    private async Task<Channel?> ResolveOneAsync(EcuadorChannel ch)
    {
        try
        {
            var http = httpFactory.CreateClient("checker");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var req = new HttpRequestMessage(HttpMethod.Head, ch.Url);
            req.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0");

            var resp = await http.SendAsync(req, cts.Token);
            var code = (int)resp.StatusCode;

            if (code is 200 or 206 or 301 or 302)
            {
                var finalUrl = code is 301 or 302 && resp.Headers.Location is not null
                    ? resp.Headers.Location.ToString()
                    : ch.Url;

                Console.WriteLine($"  ✓ {ch.Name} [HTTP {code}]");
                return new Channel
                {
                    Name        = ch.Name,
                    Group       = ch.Group,
                    ChNo        = ch.ChNo,
                    TvgId       = ch.TvgId,
                    Url         = ch.Url,
                    ResolvedUrl = finalUrl,
                    Status      = ChannelStatus.Regenerated,
                };
            }

            Console.WriteLine($"  ✗ {ch.Name} [HTTP {code}]");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ {ch.Name} → {ex.Message}");
            return null;
        }
    }

    private static async Task<bool> NordVpnConnectAsync(string country)
    {
        try
        {
            var r = await RunAsync("nordvpn", $"connect {country}");
            return r.ExitCode == 0
                || r.Output.Contains("Connected",       StringComparison.OrdinalIgnoreCase)
                || r.Output.Contains("already connected", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static async Task NordVpnDisconnectAsync()
    {
        try { await RunAsync("nordvpn", "disconnect"); }
        catch { /* best effort */ }
    }

    private static async Task<string> GetCurrentCountryAsync()
    {
        try
        {
            using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            return await c.GetStringAsync("https://ipinfo.io/country");
        }
        catch { return "??"; }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, output);
    }
}
