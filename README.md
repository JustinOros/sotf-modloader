# sotf-modloader

A web page for installing, updating and removing Sons of the Forest mods with one click.

Live site: https://justinoros.github.io/sotf-modloader/

## For players

1. Open the site in Chrome or Edge on Windows.
2. Click **Choose game folder** and pick your Sons of the Forest install folder.
3. Install RedLoader if it isn't installed yet, then install the mods you want.

The page remembers your folder. Close the game before installing or updating.

## How it works

A GitHub Actions workflow runs every 6 hours, on every push to `main`, and on demand. It:

- finds every repo owned by the account with the `sotf-mod` topic and mirrors the zip from its latest release
- mirrors the latest RedLoader release
- publishes everything with a `manifest.json` to GitHub Pages

The page writes files straight into the game folder using the browser's File System Access API.

## Setup

1. Settings, Pages, Source: **GitHub Actions**.
2. Add the `sotf-mod` topic to each mod repo.
3. Actions, **Build and deploy**, **Run workflow**.

## Mod release requirements

- The latest release has a `.zip` asset containing `<Name>.dll` and `<Name>/manifest.json`.
- `version` in `manifest.json` matches the release tag, so update detection works.
- `platform` in `manifest.json` is `Client`, `Server` or `Universal`.

After publishing a mod release, run the workflow manually to update the site right away.
