using IptvUpdater.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IptvUpdater.Services;

/// <summary>
/// Teleamazonas è geo-bloccata (HTTP 403 senza VPN Ecuador).
/// Il flusso è servito via streamlink in background su http://LOCAL_IP:8888.
/// SSIPTV e VLC sul TV si connettono all’IP locale del PC — nessun token da rigenerare.
/// </summary>
public class EcuadorResolver(EcuadorConfig config)
{
    private const int    StreamlinkPort = 8888;
    private const string StreamlinkExe  = "streamlink";
    private const string StreamlinkQuality = "best";

    public async Task<List<Channel>> ResolveAsync()
    {
        Console.WriteLine("\n[Ecuador] Connetto NordVPN...");

        if (!await NordVpnConnectAsync(config.NordVpnCountry))
        {
            Console.WriteLine(
                "  \u2717 NordVPN non disponibile — canali Ecuador saltati.\n" +
                "    Soluzione: nordvpn login && nordvpn set technology nordlynx");
            return [];
        }

        await Task.Delay(4000);

        var country = await GetCurrentCountryAsync();
        Console.WriteLine($"  IP esterno: {country.Trim()} (atteso EC o CO via server virtuale)");

        var localIp = GetLocalIp();
        Console.WriteLine($"  IP locale PC: {localIp}");

        var results = new List<Channel>();

        foreach (var ch in config.Channels)
        {
            Console.WriteLine($"  Avvio streamlink per {ch.Name}...");
            var port = StreamlinkPort + results.Count; // porta incrementale se più canali
            var started = await StartStreamlinkAsync(ch.Url, port);

            if (started)
            {
                var proxyUrl = $"http://{localIp}:{port}";
                Console.WriteLine($"  \u2713 {ch.Name} \u2192 {proxyUrl}");
                results.Add(new Channel
                {
                    Name        = ch.Name,
                    Group       = ch.Group,
                    ChNo        = ch.ChNo,
                    TvgId       = ch.TvgId,
                    Url         = proxyUrl,
                    ResolvedUrl = proxyUrl,
                    Status      = ChannelStatus.Alive,
                });
            }
            else
            {
                Console.WriteLine($"  \u2717 {ch.Name} \u2192 streamlink non avviato");
            }
        }

        // NON disconnettiamo la VPN: streamlink ne ha bisogno mentre trasmette
        if (results.Count > 0)
            Console.WriteLine("  VPN rimane connessa (streamlink attivo in background)");
        else
        {
            Console.WriteLine("  Disconnetto NordVPN (nessun canale attivo)...");
            await NordVpnDisconnectAsync();
        }

        Console.WriteLine($"  Ecuador: {results.Count}/{config.Channels.Count} ok");
        return results;
    }

    // Avvia streamlink come processo background, attende che il server sia pronto
    private static async Task<bool> StartStreamlinkAsync(string url, int port)
    {
        try
        {
            // Termina eventuale streamlink precedente sulla stessa porta
            await KillPortAsync(port);

            var psi = new ProcessStartInfo(StreamlinkExe,
                $"--player-external-http --player-external-http-port {port} \"{url}\" {StreamlinkQuality}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };

            var proc = Process.Start(psi)!;

            // Attende fino a 15s che streamlink scriva "Starting server"
            var cts     = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var ready   = false;

            _ = Task.Run(async () =>
            {
                while (!proc.StandardOutput.EndOfStream && !cts.Token.IsCancellationRequested)
                {
                    var line = await proc.StandardOutput.ReadLineAsync(cts.Token);
                    if (line is not null && line.Contains("Starting server",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ready = true;
                        cts.Cancel();
                    }
                }
            }, cts.Token);

            _ = Task.Run(async () =>
            {
                while (!proc.StandardError.EndOfStream && !cts.Token.IsCancellationRequested)
                {
                    var line = await proc.StandardError.ReadLineAsync(cts.Token);
                    if (line is not null && line.Contains("Starting server",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ready = true;
                        cts.Cancel();
                    }
                }
            }, cts.Token);

            try { await Task.Delay(15000, cts.Token); } catch (OperationCanceledException) { }

            return ready;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Errore avvio streamlink: {ex.Message}");
            return false;
        }
    }

    private static async Task KillPortAsync(int port)
    {
        try
        {
            // fuser -k porta/tcp (Linux)
            await RunAsync("fuser", $"-k {port}/tcp");
        }
        catch { /* ignora se fuser non c'è */ }
    }

    private static string GetLocalIp()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                        return ip;
                }
            }
        }
        return "127.0.0.1";
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
