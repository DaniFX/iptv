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
