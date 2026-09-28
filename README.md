# sotf-modloader

Install, update and remove Sons of the Forest mods with one click.

Live site: https://justinoros.github.io/sotf-modloader/

## For players

The site offers three ways to install:

1. **Windows app (recommended).** Download `SotfModLoader.exe` from the site and run it. It finds your game through Steam, including installs inside Program Files, and installs RedLoader and mods with one click.
2. **Zip download.** One zip with RedLoader and every mod. Extract it into your Sons of the Forest folder, the one that contains `SonsOfTheForest.exe`.
3. **Install from the page.** Works in Chrome or Edge when the game is not inside Program Files. Browsers are not allowed to open folders there.

Close the game before installing or updating.

## How it works

The `Build and deploy` workflow runs every 6 hours, on every push to `main`, and on demand. It:

- finds every repo owned by the account with the `sotf-mod` topic and mirrors the zip from its latest release
- mirrors the latest RedLoader release
- builds `downloads/sotf-mods.zip` with RedLoader and every mod
- publishes everything with a `manifest.json` to GitHub Pages

The Windows app in `app/` reads the same `manifest.json`.

## Releasing the Windows app

Push a tag starting with `app-v`:

```
git tag app-v1.0.0 && git push origin app-v1.0.0
```

The `Build Windows app` workflow builds `SotfModLoader.exe`, signs it with SignPath when signing is set up, publishes a GitHub release and refreshes the site.

## Setup

1. Settings, Pages, Source: **GitHub Actions**.
2. Add the `sotf-mod` topic to each mod repo.
3. Actions, **Build and deploy**, **Run workflow**.

## Mod release requirements

- The latest release has a `.zip` asset containing `<Name>.dll` and `<Name>/manifest.json`.
- `version` in `manifest.json` matches the release tag, so update detection works.
- `platform` in `manifest.json` is `Client`, `Server` or `Universal`.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: [JustinOros](https://github.com/JustinOros)
- Approvers: [JustinOros](https://github.com/JustinOros)

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. It downloads the mod list and mod files from this project's GitHub Pages site and GitHub releases.

## License

MIT. RedLoader is by ToniMacaroni and is redistributed unmodified under the Apache License 2.0.
