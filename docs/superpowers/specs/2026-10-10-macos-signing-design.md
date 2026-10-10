# macOS Signing & Notarization for EasyRedmineTool Releases

## Goal

GitHub Release macOS builds should open without Gatekeeper workarounds, matching the TrainArena approach: Universal Binary → `.app` bundle → Developer ID sign → notarize → staple.

## Decisions

| Topic | Choice |
|-------|--------|
| Artifact shape | Single `osx-universal` ZIP (not separate `osx-x64` / `osx-arm64`) |
| Where signing runs | `release.yml` only (tag `v*`); `prerelease.yml` stays unsigned |
| Implementation style | Port TrainArena script + docs (not inline YAML, not third-party Action) |
| Publish mode (macOS) | `PublishSingleFile=true` (simplifies codesign / notarize) |
| Secrets | Same `APPLE_*` names as TrainArena; must be configured on this repo |

## Architecture

```
tag v* → validate → release-macos (macos-14)
  → publish osx-arm64 + osx-x64 (self-contained, single-file)
  → lipo → EasyRedmineTool.Desktop (universal)
  → scripts/macos-sign-notarize.sh
       → EasyRedmineTool.app (MacOS/ + Resources/)
       → codesign (Developer ID + hardened runtime + entitlements)
       → notarytool submit --wait
       → stapler staple
  → zip EasyRedmineTool-<tag>-osx-universal.zip
  → create-release attaches assets
```

Windows and Linux jobs stay as they are.

## New / changed files

| Path | Role |
|------|------|
| `scripts/macos-sign-notarize.sh` | Keychain import, `.app` layout, sign, notarize, staple |
| `docs/macos/Info.plist.template` | Bundle id `de.thomasmenzl.easyredminetool`, executable `EasyRedmineTool.Desktop`, version placeholder, app icon if available |
| `docs/macos/EasyRedmineTool.entitlements` | JIT / unsigned executable memory / disable library validation (Avalonia/.NET); `network.client` (Redmine API). No `network.server` unless needed later |
| `docs/MACOS_SIGNING.md` | Secret setup and how to test |
| `docs/macos-starten.txt` | Short user note inside the ZIP |
| `.github/workflows/release.yml` | Replace dual RID matrix with universal + sign step |
| `README.md` | Replace “macOS (unsigned)” with signed Universal download instructions |

## Secrets (repo Actions secrets)

Identical to TrainArena:

- `APPLE_CERTIFICATE_BASE64`
- `APPLE_CERTIFICATE_PASSWORD`
- `APPLE_SIGNING_IDENTITY`
- `APPLE_API_KEY_BASE64`
- `APPLE_API_KEY_ID`
- `APPLE_API_ISSUER_ID`

If certificate or API key secrets are missing, the macOS job fails with a clear error pointing at `docs/MACOS_SIGNING.md`.

## App bundle layout

- `Contents/MacOS/EasyRedmineTool.Desktop` — universal executable only
- `Contents/Resources/` — remaining publish output (if any) + app icon
- `Contents/Info.plist` — from template with release version (`v` prefix stripped from tag)

Writable settings already use Application Support (`AppSettingsService`); nothing writes into the sealed bundle.

## Release artifact

- Name: `EasyRedmineTool-<tag>-osx-universal.zip`
- Contents: `EasyRedmineTool.app` + `macos-starten.txt`
- `create-release` continues to collect all platform artifacts; Windows/Linux names unchanged

## Out of scope

- Signing prerelease / CI builds
- App Store distribution
- Changing Windows or Linux packaging
- Automating secret creation (manual copy from TrainArena / Apple Developer)

## Success criteria

1. Tagging `v*` produces a notarized, stapled `EasyRedmineTool.app` in the universal ZIP.
2. On a clean macOS machine, double-click starts without `xattr` / “Papierkorb” Gatekeeper dialog.
3. README and `docs/MACOS_SIGNING.md` document download and secret setup.
4. Missing `APPLE_*` secrets fail the macOS release job explicitly.
