namespace IptvUpdater.Models;

public record Channel
{
    public string Name          { get; init; } = "";
    public string Url           { get; init; } = "";
    public string Group         { get; init; } = "";
    public string ChNo          { get; init; } = "";
    public string TvgId         { get; init; } = "";
    public string TvgLogo       { get; init; } = "";
    public string RawExtInf     { get; init; } = "";
    public ChannelStatus Status  { get; set; }  = ChannelStatus.Unknown;
    public string? ResolvedUrl  { get; set; }
}

public enum ChannelStatus { Unknown, Alive, Dead, Regenerated, Skipped }
