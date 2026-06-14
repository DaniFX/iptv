namespace IptvUpdater.Models;

public class AppSettings
{
    public List<SourceConfig>  Sources  { get; set; } = [];
    public GitHubConfig        GitHub   { get; set; } = new();
    public RaiConfig           Rai      { get; set; } = new();
    public EcuadorConfig       Ecuador  { get; set; } = new();
    public CheckerConfig       Checker  { get; set; } = new();
}

public class SourceConfig
{
    public string Name    { get; set; } = "";
    public string Url     { get; set; } = "";
    public bool   Enabled { get; set; } = true;
}

public class GitHubConfig
{
    public string Owner    { get; set; } = "";
    public string Repo     { get; set; } = "";
    public string Branch   { get; set; } = "main";
    public string FilePath { get; set; } = "index.m3u";
    public string Token    { get; set; } = "";
}

public class RaiConfig
{
    public string           UserAgent { get; set; } = "";
    public List<RaiChannel> Channels  { get; set; } = [];
}

public class RaiChannel
{
    public string Name  { get; set; } = "";
    public string Cont  { get; set; } = "";
    public string Group { get; set; } = "Italia";
    public string ChNo  { get; set; } = "";
    public string TvgId { get; set; } = "";
}

public class EcuadorConfig
{
    public string               NordVpnCountry { get; set; } = "Ecuador";
    public List<EcuadorChannel> Channels       { get; set; } = [];
}

public class EcuadorChannel
{
    public string Name  { get; set; } = "";
    public string Url   { get; set; } = "";
    public string Group { get; set; } = "Ecuador";
    public string ChNo  { get; set; } = "";
    public string TvgId { get; set; } = "";
}

public class CheckerConfig
{
    public int    TimeoutSeconds { get; set; } = 5;
    public int    MaxParallelism { get; set; } = 20;
    public string UserAgent      { get; set; } = "";
}
