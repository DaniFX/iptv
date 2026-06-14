using IptvUpdater.Models;
using System.Text.RegularExpressions;

namespace IptvUpdater.Services;

public static partial class M3uParser
{
    [GeneratedRegex(@"tvg-name=""([^""]*)""")]
    private static partial Regex TvgNameRegex();
    [GeneratedRegex(@",(.+)$")]
    private static partial Regex FallbackNameRegex();
    [GeneratedRegex(@"tvg-id=""([^""]*)""")]
    private static partial Regex TvgIdRegex();
    [GeneratedRegex(@"tvg-logo=""([^""]*)""")]
    private static partial Regex TvgLogoRegex();
    [GeneratedRegex(@"group-title=""([^""]*)""")]
    private static partial Regex GroupRegex();
    [GeneratedRegex(@"tvg-chno=""([^""]*)""")]
    private static partial Regex ChNoRegex();

    public static List<Channel> Parse(string content)
    {
        var channels = new List<Channel>();
        var lines    = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? pendingExtInf = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#EXTM3U",    StringComparison.Ordinal)) continue;
            if (line.StartsWith("#KODIPROP",  StringComparison.Ordinal)) continue;
            if (line.StartsWith("#EXTVLCOPT", StringComparison.Ordinal)) continue;

            if (line.StartsWith("#EXTINF", StringComparison.Ordinal))
            {
                pendingExtInf = line;
                continue;
            }

            if (line.StartsWith("http", StringComparison.Ordinal) && pendingExtInf is not null)
            {
                channels.Add(BuildChannel(pendingExtInf, line));
                pendingExtInf = null;
            }
        }

        return channels;
    }

    private static Channel BuildChannel(string extinf, string url)
    {
        var tvgName = TvgNameRegex().Match(extinf).Groups[1].Value;
        var name    = !string.IsNullOrEmpty(tvgName)
            ? tvgName
            : FallbackNameRegex().Match(extinf).Groups[1].Value.Trim();

        return new Channel
        {
            Name      = name,
            Url       = url,
            Group     = GroupRegex().Match(extinf).Groups[1].Value,
            ChNo      = ChNoRegex().Match(extinf).Groups[1].Value,
            TvgId     = TvgIdRegex().Match(extinf).Groups[1].Value,
            TvgLogo   = TvgLogoRegex().Match(extinf).Groups[1].Value,
            RawExtInf = extinf,
        };
    }

    public static string Serialize(IEnumerable<Channel> channels, string epgUrl = "")
    {
        var sb     = new System.Text.StringBuilder();
        var header = string.IsNullOrEmpty(epgUrl)
            ? "#EXTM3U"
            : $"#EXTM3U x-tvg-url=\"{epgUrl}\"";
        sb.AppendLine(header);

        foreach (var ch in channels)
        {
            var url = ch.ResolvedUrl ?? ch.Url;
            if (string.IsNullOrEmpty(url)) continue;

            if (!string.IsNullOrEmpty(ch.RawExtInf))
            {
                sb.AppendLine(ch.RawExtInf);
            }
            else
            {
                var parts = new List<string> { "#EXTINF:-1" };
                if (!string.IsNullOrEmpty(ch.TvgId))   parts.Add($"tvg-id=\"{ch.TvgId}\"");
                if (!string.IsNullOrEmpty(ch.ChNo))    parts.Add($"tvg-chno=\"{ch.ChNo}\"");
                if (!string.IsNullOrEmpty(ch.TvgLogo)) parts.Add($"tvg-logo=\"{ch.TvgLogo}\"");
                if (!string.IsNullOrEmpty(ch.Group))   parts.Add($"group-title=\"{ch.Group}\"");
                sb.AppendLine(string.Join(" ", parts) + $",{ch.Name}");
            }
            sb.AppendLine(url);
        }

        return sb.ToString();
    }
}
