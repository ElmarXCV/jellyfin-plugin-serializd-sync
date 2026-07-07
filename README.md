# Jellyfin Serializd Plugin

Auto-scrobble watched TV shows to [Serializd](https://www.serializd.com) as you play
them in Jellyfin.

Serializd has no official API. This plugin uses the same private backend the website
uses, so it may break if that changes.

## Features
- Multi-user support
- Auto scrobble TV episodes at a given percentage to Serializd
- Log to your Serializd diary with the watch date, or just mark episodes watched (per-user toggle)
- Login with your Serializd email + password (password stored encrypted, for automatic session refresh)

## Future
- Sync existing watch history
- Un-scrobble on "mark unwatched"

## Install

Add the repository in Jellyfin (Dashboard → Plugins → Repositories → +):

```
https://raw.githubusercontent.com/ElmarXCV/jellyfin-plugin-serializd/main/manifest.json
```

Install Serializd from the catalog and restart. Or side-load manually: copy
`Jellyfin.Plugin.Serializd.dll` into a `plugins/Serializd/` folder in your Jellyfin
data directory and restart.

Requires Jellyfin 10.11 and TMDB-matched libraries.

## Configuration

Dashboard → Plugins → **Serializd**: log in per user, set the scrobble percentage and
the minimum episode length.

## Build

```sh
dotnet build -c Release
```