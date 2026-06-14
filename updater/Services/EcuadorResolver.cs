using IptvUpdater.Models;
using System.Diagnostics;

namespace IptvUpdater.Services;

/// <summary>
/// Teleamazonas e Ecuavisa sono geo-bloccati (HTTP 403 senza VPN).
/// Test confermato 2026-06-14: NordVPN Ecuador #2 (server virtuale CO) → HTTP 200.
/// </summary>
public class EcuadorResolver(EcuadorConfig config)
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0";

    public async Task<List<Channel>> ResolveAsync()
    {
        Console.WriteLine("\n[Ecuador] Connetto NordVPN...");

        if (!await NordVpnConnectAsync(config.NordVpnCountry))
        {
            Console.WriteLine(
                "  \u2717 NordVPN non disponibile \u2014 canali Ecuador saltati.\n" +
                "    Soluzione: nordvpn login && nordvpn connect Ecuador");
            return [];
        }

        await Task.Delay(4000);

        var country = await GetCurrentCountryAsync();
        Console.WriteLine($"  IP corrente: {country.Trim()} (atteso EC o CO via server virtuale)");

        try
        {
            var results = new List<Channel>();
            foreach (var ch in config.Channels)
            {
                var resolved = await ResolveOneAsync(ch);
                if (resolved is not null)
                {
                    results.Add(resolved);
                    Console.WriteLine($"  \u2713 {ch.Name} \u2192 {resolved.ResolvedUrl ?? resolved.Url}");
                }
                else
                {
                    Console.WriteLine($"  \u2717 {ch.Name} \u2192 non raggiungibile con VPN");
                }
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
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);

            var resp = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, ch.Url));
            var code = (int)resp.StatusCode;

            string finalUrl = ch.Url;
            if (code is 301 or 302 or 307 or 308 && resp.Headers.Location is not null)
            {
                finalUrl = resp.Headers.Location.IsAbsoluteUri
                    ? resp.Headers.Location.ToString()
                    : new Uri(new Uri(ch.Url), resp.Headers.Location).ToString();

                var resp2 = await client.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, finalUrl));
                code = (int)resp2.StatusCode;
            }

            if (code is 200 or 206)
            {
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

            Console.WriteLine($"    HTTP {code} su {finalUrl}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Errore: {ex.Message}");
            return null;
        }
    }

    private static async Task<bool> NordVpnConnectAsync(string country)
    {
        try
        {
            var r = await RunAsync("nordvpn", $"connect {country}");
            return r.ExitCode == 0
                || r.Output.Contains("Connected",         StringComparison.OrdinalIgnoreCase)
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
