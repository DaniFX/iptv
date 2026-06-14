using System.Text.Json;
using IptvUpdater.Models;
using IptvUpdater.Services;
using Microsoft.Extensions.DependencyInjection;

// ── Config ────────────────────────────────────────────────────────────────────
var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(settingsPath))
    settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

var json     = await File.ReadAllTextAsync(settingsPath);
var settings = JsonSerializer.Deserialize<AppSettings>(json,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("appsettings.json non valido");

if (string.IsNullOrEmpty(settings.GitHub.Token))
    settings.GitHub.Token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";

if (string.IsNullOrEmpty(settings.GitHub.Token))
{
    Console.Error.WriteLine("Token GitHub mancante. Imposta: export GITHUB_TOKEN=ghp_xxx");
    return 1;
}

// ── HTTP Client ───────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddHttpClient("checker", c =>
{
    c.Timeout = TimeSpan.FromSeconds(settings.Checker.TimeoutSeconds + 3);
    c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", settings.Checker.UserAgent);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

var provider    = services.BuildServiceProvider();
var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

// ── Servizi ───────────────────────────────────────────────────────────────────
var checker    = new LinkChecker(httpFactory, settings.Checker);
var raiRes     = new RaiResolver(settings.Rai);
var ecuadorRes = new EcuadorResolver(settings.Ecuador);
var publisher  = new GitHubPublisher(settings.GitHub);

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"IPTV Updater .NET 10 \u2014 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// ── Step 1: Scarica sorgenti ──────────────────────────────────────────────────
var allChannels = new List<Channel>();
var seenUrls    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var source in settings.Sources.Where(s => s.Enabled))
{
    Console.WriteLine($"\n[{source.Name}] Scarico {source.Url}...");
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var content = await http.GetStringAsync(source.Url);
        var parsed  = M3uParser.Parse(content);
        var added   = 0;

        foreach (var ch in parsed)
        {
            if (ch.Url.Contains("relinkerServlet", StringComparison.OrdinalIgnoreCase)) continue;
            if (ch.Url.Contains("teleamazonas",   StringComparison.OrdinalIgnoreCase)) continue;
            if (ch.Url.Contains("ecuavisa",       StringComparison.OrdinalIgnoreCase)) continue;
            if (ch.Url.Contains("vustreams",      StringComparison.OrdinalIgnoreCase)) continue;
            if (ch.Url.Contains("mdstrm.com",     StringComparison.OrdinalIgnoreCase) &&
                ch.Name.Contains("Ecuavisa",       StringComparison.OrdinalIgnoreCase)) continue;
            if (!seenUrls.Add(ch.Url)) continue;
            allChannels.Add(ch);
            added++;
        }

        Console.WriteLine($"  +{added} canali unici (tot: {allChannels.Count})");
    }
    catch (Exception ex) { Console.WriteLine($"  \u2717 {ex.Message}"); }
}

// ── Step 2: Test link ─────────────────────────────────────────────────────────
Console.WriteLine(
    $"\n[CHECK] Test {allChannels.Count} link " +
    $"(max {settings.Checker.MaxParallelism} paralleli)...");
var checked_ = await checker.CheckAllAsync(allChannels);
var alive    = checked_.Where(c => c.Status == ChannelStatus.Alive).ToList();
var dead     = checked_.Where(c => c.Status == ChannelStatus.Dead).ToList();
Console.WriteLine($"  Vivi: {alive.Count} | Morti: {dead.Count}");

if (dead.Count > 0)
    await File.WriteAllLinesAsync("/tmp/iptv_dead.log",
        dead.Select(c => $"{c.Name}: {c.Url}"));

// ── Step 3: Token RAI ─────────────────────────────────────────────────────────
var raiChannels = await raiRes.ResolveAsync();
Console.WriteLine($"  RAI: {raiChannels.Count}/{settings.Rai.Channels.Count} ok");

// ── Step 4: Ecuador NordVPN ───────────────────────────────────────────────────
var ecuadorChannels = await ecuadorRes.ResolveAsync();
Console.WriteLine($"  Ecuador: {ecuadorChannels.Count}/{settings.Ecuador.Channels.Count} ok");

// ── Step 5: Assembla M3U ──────────────────────────────────────────────────────
Console.WriteLine("\n[ASSEMBLE] Creo M3U finale...");
var final = new List<Channel>();
final.AddRange(alive);
final.AddRange(raiChannels);
final.AddRange(ecuadorChannels);

var m3u = M3uParser.Serialize(final, "https://iptv-epg.org/files/epg-it.xml");
await File.WriteAllTextAsync("/tmp/playlist_generated.m3u", m3u);
Console.WriteLine($"  Canali: {final.Count} | Copia: /tmp/playlist_generated.m3u");

// ── Step 6: Push GitHub ───────────────────────────────────────────────────────
Console.WriteLine("\n[GITHUB] Pubblico...");
await publisher.PublishAsync(m3u);

Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"Fine: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
return 0;
