# MogFlix

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that shows what
you're currently watching on Plex as your nameplate title, in the server info bar, and
(optionally) visible to other players via [Honorific](https://github.com/Caraxi/Honorific).

Licensed under [AGPL-3.0](https://www.gnu.org/licenses/agpl-3.0.html).

It polls your Plex Media Server's `/status/sessions` endpoint (the same one Plex uses
internally to show "what's playing" in its own dashboard) and displays the title,
play state, and progress bar in an ImGui window docked wherever you like on screen.

## For end users: installing without building it yourself

If you just want to use this plugin (not develop it), you don't need any of the steps
above. Instead:

1. Open `/xlsettings` in-game → **Experimental** tab → **Custom Plugin Repositories**.
2. Paste this URL into an empty field:
   `https://raw.githubusercontent.com/sadoniaveletoria/MogFlix/main/repo.json`
3. Click the `+` button, make sure it's checked **Enabled**, then **Save and Close**.
4. Open `/xlplugins` → **All Plugins**, search "MogFlix", and install it normally.

Updates will show up automatically through the regular plugin installer from then on -
no manual rebuilding needed.

## What you need

- FFXIV with [XIVLauncher](https://xivlauncher.app/) + Dalamud already installed and working.
- .NET 8 SDK (https://dotnet.microsoft.com/download).
- A Plex Media Server you have admin access to (local or on your LAN).
- Your Plex **X-Plex-Token**.

## 1. Get your Plex token

The easiest way:

1. Open Plex Web App, play any item.
2. Click the "..." menu on the item → **Get Info** → **View XML**.
3. Look at the URL in the address bar — it will contain `X-Plex-Token=xxxxxxxxxxxxxxxxxxxx`.
4. Copy everything after `X-Plex-Token=`. That's your token.

(Alternatively: Plex support article "Finding an authentication token / X-Plex-Token" walks
through a few other methods if that one doesn't work for your setup.)

Treat this token like a password — anyone with it can query your server.

## 2. Find your Plex server URL

- If Plex runs on the same PC as FFXIV: `http://127.0.0.1:32400`
- If Plex runs on another machine on your LAN: `http://<that machine's local IP>:32400`
  (e.g. `http://192.168.1.50:32400`)

You do **not** need port forwarding or a public plex.tv URL for this — the plugin just
needs to reach the server on your local network.

## 5. Use it

- `/mogflix` — open the settings window.

Once configured, the overlay will show the movie/episode title, play/pause state,
and a progress bar, refreshing on whatever poll interval you set (default 5s).

## 6. Show your title as a nameplate title

By default, only the DTR bar entry is on - it's local-only (just for you). To show a title under your character's nameplate, visible to **both you and
other nearby players**, this plugin hands off to
[**Honorific**](https://github.com/Caraxi/Honorific) (by Caraxi), a separate, widely-used
Dalamud plugin for custom player titles. **You must install Honorific yourself** from the
Dalamud plugin installer (`/xlplugins`, search "Honorific") - this plugin does not include
or replace it. Dalamud's own nameplate API has no way to broadcast text to other players'
clients on its own, which is why this route goes through Honorific instead.

Once Honorific is installed and enabled:

1. Open `/mogflix`.
2. Check **"Share via Honorific"**.
3. It'll show "Connected to Honorific" if detected, or a warning if it can't find it.

With this on, an animated gradient title shows on your nameplate for anyone nearby who
can see it (including yourself).

## Notes / things you might want to tweak

- **Multiple people on one Plex server**: set "Filter by username" in settings so it
  only shows sessions for your account.
- **Movies vs. TV**: it currently reports on `movie` and `episode` session types.
  Music sessions are ignored — easy to add in `PlexService.Poll()` if you want that too.
- **Remote/Plex Cloud access**: if your server isn't on your LAN, you can point
  `PlexServerUrl` at your public plex.tv-relayed URL instead, but that's outside
  the scope of this basic setup.

## Project structure

```
MogFlix/
  MogFlix.csproj
  Plugin.cs                  <- entry point, commands, window wiring
  Configuration.cs           <- persisted settings (server URL, token, etc.)
  Services/
    PlexModels.cs             <- XML models for Plex's session response
    PlexService.cs            <- background poller, HTTP calls to Plex
    DtrBarService.cs           <- server info bar (DTR) entry
  Integrations/
    HonorificService.cs        <- shares the title via Honorific's IPC
  Windows/
    ConfigWindow.cs            <- settings UI
```
