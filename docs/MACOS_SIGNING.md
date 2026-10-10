# macOS Signing & Notarization (GitHub Releases)

EasyRedmineTool-Downloads von GitHub brauchen **Developer ID Application** + Notarisierung (nicht App-Store-`Apple Distribution`).

CI: `.github/workflows/release.yml` baut auf `macos-14` ein **Universal Binary** (`osx-arm64` + `osx-x64` via `lipo`), packt **`EasyRedmineTool.app`**, signiert mit Developer ID, notarized und **stapelt** das Ticket (`scripts/macos-sign-notarize.sh`). So akzeptiert Gatekeeper Downloads ohne den „Papierkorb“-Dialog.

Schreibbare Daten liegen unter `~/Library/Application Support/EasyRedmineTool/` — nicht im App-Bundle.

## Secrets (Repo → Settings → Secrets and variables → Actions)

Am Mac (einmalig), analog TrainArena — dieselben Zertifikat-/API-Key-Werte können wiederverwendet werden:

```bash
# .p12 → Base64 in die Zwischenablage
base64 -i DeveloperID.p12 | pbcopy

# App Store Connect API Key .p8
base64 -i ~/.appstoreconnect/private_keys/AuthKey_XXXXXXXXXX.p8 | pbcopy

# Identity-String
security find-identity -v -p codesigning
```

| Secret | Inhalt |
|--------|--------|
| `APPLE_CERTIFICATE_BASE64` | Base64 der `.p12` (Developer ID Application) |
| `APPLE_CERTIFICATE_PASSWORD` | Passwort der `.p12` |
| `APPLE_SIGNING_IDENTITY` | z. B. `Developer ID Application: Thomas Menzl (TEAMID)` |
| `APPLE_API_KEY_BASE64` | Base64 der `.p8` |
| `APPLE_API_KEY_ID` | Key-ID |
| `APPLE_API_ISSUER_ID` | Issuer-UUID aus App Store Connect |

API-Key-Rechte: Zugang für Notary (`notarytool`).

## Test

1. Secrets im EasyRedmineTool-Repo setzen (kopieren von TrainArena möglich)
2. Tag `v*` pushen → Release-Asset `EasyRedmineTool-*-osx-universal.zip`
3. Auf dem Mac entpacken und `EasyRedmineTool.app` starten (ohne `xattr`-Workaround)

## Lokal

Lokal signieren ist optional; die Pipeline nutzt einen temporären Keychain.
