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

## For maintainers: cutting a new release

GitHub Actions (`.github/workflows/release.yml`) builds and publishes automatically
whenever a tag starting with `v` is pushed:

```bash
git tag v1.0.1
git push origin v1.0.1
```

That builds the plugin, packages it, and attaches the zip to a new GitHub Release.
`repo.json` always points at `.../releases/latest/download/MogFlix.zip`, so it
never needs manual updates between releases - just bump `AssemblyVersion` in
`MogFlix.csproj` before tagging so Dalamud's installer knows an update is
available.

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

## 3. Build the plugin

```bash
cd MogFlix
dotnet build -c Release
```

The `Dalamud.NET.Sdk` package reference in the `.csproj` automatically pulls in the
Dalamud/ImGui assemblies you need to compile against — you don't need to manually
reference your game install. If `dotnet build` complains it can't find the SDK, you may
need a newer/older version than `11.0.0` in the `<Project Sdk="Dalamud.NET.Sdk/11.0.0">`
line at the top of `MogFlix.csproj` — check the current version at
https://github.com/goatcorp/Dalamud.NET.Sdk for whatever matches your installed Dalamud.

This produces a `bin/Release/net8.0-windows/MogFlix.dll` (plus a generated
`MogFlix.json` manifest next to it, courtesy of DalamudPackager).

## 4. Load it in-game as a dev plugin

1. In-game, open Dalamud Settings (`/xlsettings`) → **Experimental** tab.
2. Under **Dev Plugin Locations**, add the path to your
   `bin/Release/net8.0-windows/` (or `Debug`) folder.
3. Open the plugin installer (`/xlplugins`) → **Dev Tools** tab → find
   **MogFlix** → click **Load**.

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

## 7. Let others browse who's watching and request to join

Sharing via Honorific only shows a nameplate title - it can't let someone browse a list
of active watchers or ask to join. That needs a small backend, since Dalamud has no
built-in way to query "who else is playing this plugin right now."

The `mogflix-presence` folder (alongside this plugin's source) is a small Cloudflare
Worker that provides exactly that: a presence list with automatic expiry, and a
request/accept/decline flow for joining someone. See its own README for deployment
steps (short version: `npm install && npx wrangler login && npx wrangler deploy`).

Once deployed:

1. Open `/mogflix`, find **"See who's watching"**, and paste your Worker's URL.
2. The shared API key is baked into the plugin's code rather than a settings field - if
   you rebuild from source, make sure `PresenceApiKey` in `PresenceService.cs` matches
   whatever you set as `PRESENCE_API_KEY` on the Worker.
3. Check **"Let others see me and request to join"**.
4. Use **"Browse Who's Watching"** (or `/mogflix browse`) to see others and send requests.

Nobody's actual Kosmi link is ever exposed through the browse list - it's only sent
directly to a requester once you accept their request.

## Notes / things you might want to tweak

- **Multiple people on one Plex server**: set "Filter by username" in settings so it
  only shows sessions for your account.
- **Movies vs. TV**: it currently reports on `movie` and `episode` session types.
  Music sessions are ignored — easy to add in `PlexService.Poll()` if you want that too.
- **Remote/Plex Cloud access**: if your server isn't on your LAN, you can point
  `PlexServerUrl` at your public plex.tv-relayed URL instead, but that's outside
  the scope of this basic setup.
- **Positioning the window**: right-click the window titlebar isn't exposed by
  default in Dalamud's ImGui windows, but you can drag it anywhere and Dalamud
  will remember its position between sessions automatically once you set
  `RespectCloseHotkey`/position persistence — or just leave `AlwaysAutoResize`
  and drag it to a corner once.

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
    KosmiJoinService.cs        <- self-only "Join My Movie" shortcut
    PresenceService.cs         <- talks to the mogflix-presence Worker
  Windows/
    ConfigWindow.cs             <- settings UI
    BrowseWindow.cs             <- browse/request-to-join list
    IncomingRequestWindow.cs    <- accept/decline popup

mogflix-presence/              <- separate Cloudflare Worker (own README)
  src/index.ts
  src/PresenceRoom.ts
  wrangler.toml
```
