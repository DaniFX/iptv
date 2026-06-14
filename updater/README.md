# IptvUpdater (.NET 10)

App console che aggiorna automaticamente `index.m3u` in questa repo.

## Cosa fa

1. Scarica le M3U da **DaniFX/iptv** e **maginetweb-arch/TVITALIA**
2. Deduplica per URL
3. Testa ogni link in parallelo (rimuove i morti)
4. Rigenera i **token RAI** via relinker
5. Risolve **Teleamazonas + Ecuavisa** via **NordVPN Ecuador**
6. Pubblica `index.m3u` aggiornato su questa repo via GitHub API

## Requisiti

- .NET 10 SDK
- `nordvpn` client installato e autenticato (`nordvpn login`)
- GitHub Personal Access Token con permesso `contents:write`

## Setup

```bash
cd updater
export GITHUB_TOKEN=ghp_xxxxxxxxxxxx
dotnet run
```

## Cron ogni 20 minuti

```cron
*/20 * * * * cd /percorso/iptv/updater && GITHUB_TOKEN=ghp_xxx dotnet run >> /tmp/iptv.log 2>&1
```

## URL per SS IPTV

```
https://raw.githubusercontent.com/DaniFX/iptv/main/index.m3u
```

## Note Teleamazonas / Ecuavisa (geo-block)

Test confermato il 2026-06-14:

| Condizione | HTTP | Stream |
|---|---|---|
| Senza VPN | 403 | ✗ bloccato |
| NordVPN Ecuador #2 (virtuale Colombia) | 200 | ✓ funziona |

**NordVPN assegna un server virtuale in Colombia** (`ec2.nordvpn.com`) che supera
il geo-block del CDN `vustreams.com`. Ecuavisa (mdstrm.com) è da verificare
con lo stesso script.

Per guardare in VLC:

```bash
nordvpn connect Ecuador && sleep 3
vlc "https://teleamazonas-live.cdn.vustreams.com/live/fd4ab346-b4e3-4628-abf0-b5a1bc192428/live.isml/playlist.m3u8" \
  --http-user-agent "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0"
```

Per testare entrambi i canali:

```bash
bash scripts/test-ecuador.sh
```

## Struttura

```
updater/
├── Program.cs
├── appsettings.json
├── IptvUpdater.csproj
├── README.md
├── Models/
│   ├── Channel.cs
│   └── AppSettings.cs
└── Services/
    ├── M3uParser.cs
    ├── LinkChecker.cs
    ├── RaiResolver.cs
    ├── EcuadorResolver.cs
    └── GitHubPublisher.cs

scripts/
└── test-ecuador.sh
```
