# macOS Signing & Notarization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tag releases (`v*`) publish a notarized, stapled Universal `EasyRedmineTool.app` in `EasyRedmineTool-<tag>-osx-universal.zip`, matching TrainArena.

**Architecture:** Port TrainArena’s `scripts/macos-sign-notarize.sh` + `docs/macos/*` into this repo. `release.yml` builds `osx-arm64` + `osx-x64` with `PublishSingleFile=true`, merges via `lipo`, then signs/notarizes/staples. Prerelease stays unsigned.

**Tech Stack:** GitHub Actions (`macos-14`), `codesign`, `notarytool`, `stapler`, `lipo`, Developer ID Application + App Store Connect API key (repo secrets).

## Global Constraints

- Sign only in `.github/workflows/release.yml` (not `prerelease.yml` / `ci.yml`).
- Artifact: single `EasyRedmineTool-<tag>-osx-universal.zip` containing `EasyRedmineTool.app` + `macos-starten.txt`.
- Secrets: `APPLE_CERTIFICATE_BASE64`, `APPLE_CERTIFICATE_PASSWORD`, `APPLE_SIGNING_IDENTITY`, `APPLE_API_KEY_BASE64`, `APPLE_API_KEY_ID`, `APPLE_API_ISSUER_ID`.
- Bundle id: `de.thomasmenzl.easyredminetool`.
- Executable name inside the bundle: `EasyRedmineTool.Desktop`.
- macOS publish: `--self-contained true` and `-p:PublishSingleFile=true`.
- Spec: `docs/superpowers/specs/2026-10-10-macos-signing-design.md`.

## File map

| Path | Responsibility |
|------|----------------|
| `docs/macos/Info.plist.template` | App bundle metadata + version placeholder |
| `docs/macos/EasyRedmineTool.entitlements` | Hardened-runtime entitlements for Avalonia/.NET |
| `docs/macos-starten.txt` | Short start note shipped in the ZIP |
| `docs/MACOS_SIGNING.md` | How to configure secrets and verify |
| `scripts/macos-sign-notarize.sh` | Keychain, `.app` layout, sign, notarize, staple |
| `.github/workflows/release.yml` | Universal macOS job + invoke script |
| `README.md` | Document signed Universal download |

---

### Task 1: macOS bundle metadata and docs

**Files:**
- Create: `docs/macos/Info.plist.template`
- Create: `docs/macos/EasyRedmineTool.entitlements`
- Create: `docs/macos-starten.txt`
- Create: `docs/MACOS_SIGNING.md`

**Interfaces:**
- Consumes: none
- Produces: plist template with `__VERSION__`; entitlements path used by the signing script; docs for operators

- [ ] **Step 1: Create `docs/macos/` directory**

```bash
mkdir -p docs/macos
```

- [ ] **Step 2: Write `docs/macos/Info.plist.template`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>de</string>
	<key>CFBundleExecutable</key>
	<string>EasyRedmineTool.Desktop</string>
	<key>CFBundleIdentifier</key>
	<string>de.thomasmenzl.easyredminetool</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>EasyRedmineTool</string>
	<key>CFBundleDisplayName</key>
	<string>EasyRedmineTool</string>
	<key>CFBundleIconFile</key>
	<string>AppIcon</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>__VERSION__</string>
	<key>CFBundleVersion</key>
	<string>__VERSION__</string>
	<key>LSMinimumSystemVersion</key>
	<string>12.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSHumanReadableCopyright</key>
	<string>Copyright © Thomas Menzl</string>
</dict>
</plist>
```

- [ ] **Step 3: Write `docs/macos/EasyRedmineTool.entitlements`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.security.cs.allow-jit</key>
	<true/>
	<key>com.apple.security.cs.allow-unsigned-executable-memory</key>
	<true/>
	<key>com.apple.security.cs.disable-library-validation</key>
	<true/>
	<key>com.apple.security.network.client</key>
	<true/>
</dict>
</plist>
```

- [ ] **Step 4: Write `docs/macos-starten.txt`**

```text
EasyRedmineTool auf macOS starten
=================================

1. ZIP entpacken
2. EasyRedmineTool.app per Doppelklick starten
   (signiert + notarized + stapled — kein Gatekeeper-„Papierkorb“-Dialog)

Einstellungen/API-Schlüssel:
  ~/Library/Application Support/EasyRedmineTool/settings.json

Universal Binary (Apple Silicon + Intel): EasyRedmineTool-*-osx-universal.zip

Details: docs/MACOS_SIGNING.md
```

- [ ] **Step 5: Write `docs/MACOS_SIGNING.md`**

```markdown
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
```

- [ ] **Step 6: Commit**

```bash
git add docs/macos/Info.plist.template docs/macos/EasyRedmineTool.entitlements docs/macos-starten.txt docs/MACOS_SIGNING.md
git commit -m "$(cat <<'EOF'
Add macOS app bundle metadata and signing docs.

EOF
)"
```

---

### Task 2: Signing and notarization script

**Files:**
- Create: `scripts/macos-sign-notarize.sh`

**Interfaces:**
- Consumes: publish dir containing `EasyRedmineTool.Desktop`; env secrets listed in Global Constraints; optional `APP_VERSION`; optional `ENTITLEMENTS_PATH`
- Produces: `EasyRedmineTool.app` in the publish dir (signed, notarized, stapled)
- Uses: `docs/macos/Info.plist.template`, `docs/macos/EasyRedmineTool.entitlements`, `src/EasyRedmineTool.Desktop/Assets/app-icon.icns` → `Contents/Resources/AppIcon.icns`

- [ ] **Step 1: Create `scripts/` and write `scripts/macos-sign-notarize.sh`**

```bash
#!/usr/bin/env bash
# Wrap publish output in EasyRedmineTool.app, Developer-ID-sign, notarize, and staple.
# Layout: MacOS/ = executable only; Resources/ = remaining publish output + icon.
# Required env:
#   APPLE_CERTIFICATE_BASE64, APPLE_CERTIFICATE_PASSWORD, APPLE_SIGNING_IDENTITY
#   APPLE_API_KEY_BASE64, APPLE_API_KEY_ID, APPLE_API_ISSUER_ID
# Optional: APP_VERSION (defaults to 0.0.0)
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <publish-dir>" >&2
  exit 2
fi

PUBLISH_DIR=$(cd "$1" && pwd)
BINARY="$PUBLISH_DIR/EasyRedmineTool.Desktop"
ROOT=$(cd "$(dirname "$0")/.." && pwd)
ENTITLEMENTS="${ENTITLEMENTS_PATH:-$ROOT/docs/macos/EasyRedmineTool.entitlements}"
PLIST_TEMPLATE="$ROOT/docs/macos/Info.plist.template"
ICON_SRC="$ROOT/src/EasyRedmineTool.Desktop/Assets/app-icon.icns"
APP_VERSION="${APP_VERSION:-0.0.0}"
APP_BUNDLE="$PUBLISH_DIR/EasyRedmineTool.app"

for var in APPLE_CERTIFICATE_BASE64 APPLE_CERTIFICATE_PASSWORD APPLE_SIGNING_IDENTITY \
           APPLE_API_KEY_BASE64 APPLE_API_KEY_ID APPLE_API_ISSUER_ID; do
  if [[ -z "${!var:-}" ]]; then
    echo "Missing env: $var" >&2
    exit 1
  fi
done

if [[ ! -f "$BINARY" ]]; then
  echo "Binary not found: $BINARY" >&2
  exit 1
fi
if [[ ! -f "$ENTITLEMENTS" || ! -f "$PLIST_TEMPLATE" ]]; then
  echo "Missing entitlements or Info.plist template under docs/macos/" >&2
  exit 1
fi

KEYCHAIN="easyredminetool-ci.keychain-db"
KEYCHAIN_PW=$(uuidgen | tr '[:upper:]' '[:lower:]')
CERT_PATH=$(mktemp "${TMPDIR:-/tmp}/easyredminetool-cert.XXXXXX.p12")
API_KEY_PATH=$(mktemp "${TMPDIR:-/tmp}/AuthKey.XXXXXX.p8")
NOTARY_ZIP=$(mktemp "${TMPDIR:-/tmp}/easyredminetool-notarize.XXXXXX.zip")
STAGING=$(mktemp -d "${TMPDIR:-/tmp}/easyredminetool-app.XXXXXX")

cleanup() {
  security delete-keychain "$KEYCHAIN" 2>/dev/null || true
  rm -f "$CERT_PATH" "$API_KEY_PATH" "$NOTARY_ZIP"
  rm -rf "$STAGING"
}
trap cleanup EXIT

echo "$APPLE_CERTIFICATE_BASE64" | base64 --decode > "$CERT_PATH"
echo "$APPLE_API_KEY_BASE64" | base64 --decode > "$API_KEY_PATH"

security delete-keychain "$KEYCHAIN" 2>/dev/null || true
security create-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security import "$CERT_PATH" -k "$KEYCHAIN" -P "$APPLE_CERTIFICATE_PASSWORD" \
  -A -T /usr/bin/codesign -T /usr/bin/security -T /usr/bin/productsign

EXISTING=$(security list-keychains -d user | sed 's/"//g' | tr '\n' ' ')
security list-keychains -d user -s "$KEYCHAIN" $EXISTING
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PW" "$KEYCHAIN"

rm -rf "$APP_BUNDLE"
MACOS_DIR="$STAGING/EasyRedmineTool.app/Contents/MacOS"
RES_DIR="$STAGING/EasyRedmineTool.app/Contents/Resources"
mkdir -p "$MACOS_DIR" "$RES_DIR"

# Executable only in MacOS/; everything else in Resources/ (required for codesign).
mv "$BINARY" "$MACOS_DIR/EasyRedmineTool.Desktop"
chmod +x "$MACOS_DIR/EasyRedmineTool.Desktop"
shopt -s dotglob nullglob
for item in "$PUBLISH_DIR"/*; do
  base=$(basename "$item")
  [[ "$base" == "EasyRedmineTool.app" ]] && continue
  mv "$item" "$RES_DIR/"
done
shopt -u dotglob nullglob

if [[ -f "$ICON_SRC" ]]; then
  cp "$ICON_SRC" "$RES_DIR/AppIcon.icns"
fi

sed "s/__VERSION__/${APP_VERSION//\//\\/}/g" "$PLIST_TEMPLATE" \
  > "$STAGING/EasyRedmineTool.app/Contents/Info.plist"

codesign --force --options runtime --timestamp \
  --keychain "$KEYCHAIN" \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$MACOS_DIR/EasyRedmineTool.Desktop"

codesign --force --options runtime --timestamp \
  --keychain "$KEYCHAIN" \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$STAGING/EasyRedmineTool.app"

codesign --verify --deep --strict --verbose=2 "$STAGING/EasyRedmineTool.app"
codesign -dv --verbose=2 "$STAGING/EasyRedmineTool.app" 2>&1 || true

rm -f "$NOTARY_ZIP"
ditto -c -k --keepParent "$STAGING/EasyRedmineTool.app" "$NOTARY_ZIP"
xcrun notarytool submit "$NOTARY_ZIP" \
  --key "$API_KEY_PATH" \
  --key-id "$APPLE_API_KEY_ID" \
  --issuer "$APPLE_API_ISSUER_ID" \
  --wait

xcrun stapler staple "$STAGING/EasyRedmineTool.app"
xcrun stapler validate "$STAGING/EasyRedmineTool.app"

mv "$STAGING/EasyRedmineTool.app" "$APP_BUNDLE"
echo "Signed, notarized, and stapled: $APP_BUNDLE"
```

- [ ] **Step 2: Make the script executable**

```bash
chmod +x scripts/macos-sign-notarize.sh
```

- [ ] **Step 3: Sanity-check script syntax (no Apple secrets required)**

```bash
bash -n scripts/macos-sign-notarize.sh
```

Expected: no output, exit code 0.

- [ ] **Step 4: Commit**

```bash
git add scripts/macos-sign-notarize.sh
git commit -m "$(cat <<'EOF'
Add macOS sign and notarize script for release builds.

EOF
)"
```

---

### Task 3: Wire Universal signed build into `release.yml`

**Files:**
- Modify: `.github/workflows/release.yml` (replace `release-macos` job; keep validate / windows / linux / create-release)

**Interfaces:**
- Consumes: `scripts/macos-sign-notarize.sh`, `docs/macos-starten.txt`, tag `github.ref_name`
- Produces: artifact `EasyRedmineTool-${{ github.ref_name }}-osx-universal` (ZIP path used by `create-release`)

- [ ] **Step 1: Replace the `release-macos` job with the following**

Delete the current matrix job (`rid: [osx-x64, osx-arm64]`) and use this single job:

```yaml
  release-macos:
    needs: validate
    runs-on: macos-14

    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x

      - name: Publish macOS arm64 and x64 (single-file)
        run: |
          set -euo pipefail
          for rid in osx-arm64 osx-x64; do
            dotnet publish src/EasyRedmineTool.Desktop/EasyRedmineTool.Desktop.csproj \
              --configuration Release \
              --runtime "$rid" \
              --self-contained true \
              -p:PublishSingleFile=true \
              --output "publish/$rid"
          done

      - name: Create universal binary
        run: |
          set -euo pipefail
          rm -rf publish/osx-universal
          cp -R publish/osx-arm64 publish/osx-universal
          lipo -create \
            publish/osx-arm64/EasyRedmineTool.Desktop \
            publish/osx-x64/EasyRedmineTool.Desktop \
            -output publish/osx-universal/EasyRedmineTool.Desktop
          chmod +x publish/osx-universal/EasyRedmineTool.Desktop
          lipo -info publish/osx-universal/EasyRedmineTool.Desktop
          rm -rf publish/osx-arm64 publish/osx-x64

      - name: Sign and notarize
        env:
          APPLE_CERTIFICATE_BASE64: ${{ secrets.APPLE_CERTIFICATE_BASE64 }}
          APPLE_CERTIFICATE_PASSWORD: ${{ secrets.APPLE_CERTIFICATE_PASSWORD }}
          APPLE_SIGNING_IDENTITY: ${{ secrets.APPLE_SIGNING_IDENTITY }}
          APPLE_API_KEY_BASE64: ${{ secrets.APPLE_API_KEY_BASE64 }}
          APPLE_API_KEY_ID: ${{ secrets.APPLE_API_KEY_ID }}
          APPLE_API_ISSUER_ID: ${{ secrets.APPLE_API_ISSUER_ID }}
        run: |
          set -euo pipefail
          if [[ -z "${APPLE_CERTIFICATE_BASE64}" || -z "${APPLE_API_KEY_BASE64}" ]]; then
            echo "::error::macOS signing secrets missing. See docs/MACOS_SIGNING.md"
            exit 1
          fi
          export APP_VERSION="${GITHUB_REF_NAME#v}"
          chmod +x scripts/macos-sign-notarize.sh
          scripts/macos-sign-notarize.sh "publish/osx-universal"

      - name: Create ZIP archive
        run: |
          set -euo pipefail
          version="${{ github.ref_name }}"
          archive="EasyRedmineTool-${version}-osx-universal.zip"
          cp docs/macos-starten.txt publish/osx-universal/macos-starten.txt
          (cd publish/osx-universal && zip -ry "../../${archive}" EasyRedmineTool.app macos-starten.txt)
          echo "ARCHIVE_NAME=${archive}" >> "$GITHUB_ENV"
          ls -lh "${archive}"

      - name: Upload build artifact
        uses: actions/upload-artifact@v6
        with:
          name: EasyRedmineTool-${{ github.ref_name }}-osx-universal
          path: ${{ env.ARCHIVE_NAME }}
          retention-days: 90
```

Keep `create-release` as-is (`pattern: EasyRedmineTool-*`); it will pick up the new universal name automatically.

Do **not** change `prerelease.yml` in this task (unsigned prereleases remain intentional).

- [ ] **Step 2: YAML sanity check**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml')); print('ok')"
```

Expected: `ok`. If PyYAML is missing, use: `ruby -ryaml -e "YAML.load_file('.github/workflows/release.yml'); puts 'ok'"`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "$(cat <<'EOF'
Sign and notarize universal macOS release builds.

EOF
)"
```

---

### Task 4: Update README macOS download docs

**Files:**
- Modify: `README.md` (release artifact table + macOS section around the “unsigned” instructions)

**Interfaces:**
- Consumes: new artifact name from Task 3
- Produces: user-facing install steps for signed `.app`

- [ ] **Step 1: Update the release artifact table**

Replace the two macOS rows:

```markdown
| **macOS Intel** | `EasyRedmineTool-v*-osx-x64.zip` |
| **macOS Apple Silicon** | `EasyRedmineTool-v*-osx-arm64.zip` |
```

with:

```markdown
| **macOS (Universal)** | `EasyRedmineTool-v*-osx-universal.zip` |
```

- [ ] **Step 2: Replace the “macOS (unsigned)” section**

Replace from `### macOS (unsigned)` through the unzip/`chmod` example with:

```markdown
### macOS

ZIP entpacken und `EasyRedmineTool.app` per Doppelklick starten (signiert, notarisiert und stapled).

```bash
unzip EasyRedmineTool-v*-osx-universal.zip -d EasyRedmineTool
open EasyRedmineTool/EasyRedmineTool.app
```

Universal Binary: läuft auf Apple Silicon und Intel. Details zur Signierung: [docs/MACOS_SIGNING.md](docs/MACOS_SIGNING.md).
```

Also add `docs/MACOS_SIGNING.md` under “Weitere Dokumentation” if that list exists.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "$(cat <<'EOF'
Document signed universal macOS release downloads.

EOF
)"
```

---

### Task 5: Operator checklist (secrets) and verification notes

**Files:**
- No code changes required if Tasks 1–4 are done
- Operator action outside git: copy `APPLE_*` secrets from TrainArena into `tom4711/EasyRedmineTool`

- [ ] **Step 1: Confirm secrets on the GitHub repo**

```bash
gh secret list -R tom4711/EasyRedmineTool
```

Expected: the six `APPLE_*` names listed. If missing, set them (values from TrainArena / local keychain — do not commit values):

```bash
# Example pattern only — paste real values interactively:
# gh secret set APPLE_CERTIFICATE_BASE64 -R tom4711/EasyRedmineTool
# gh secret set APPLE_CERTIFICATE_PASSWORD -R tom4711/EasyRedmineTool
# gh secret set APPLE_SIGNING_IDENTITY -R tom4711/EasyRedmineTool
# gh secret set APPLE_API_KEY_BASE64 -R tom4711/EasyRedmineTool
# gh secret set APPLE_API_KEY_ID -R tom4711/EasyRedmineTool
# gh secret set APPLE_API_ISSUER_ID -R tom4711/EasyRedmineTool
```

- [ ] **Step 2: After merge to `main`, verify with a real tag release**

```bash
# From a clean main (after PR merge), e.g.:
git tag v0.11.0
git push origin v0.11.0
gh run watch --repo tom4711/EasyRedmineTool
```

Expected: `release-macos` succeeds; Release asset `EasyRedmineTool-v0.11.0-osx-universal.zip` present.

On a Mac:

```bash
unzip EasyRedmineTool-v0.11.0-osx-universal.zip -d /tmp/ert-check
codesign -dv --verbose=2 /tmp/ert-check/EasyRedmineTool.app
spctl -a -vvv -t install /tmp/ert-check/EasyRedmineTool.app
open /tmp/ert-check/EasyRedmineTool.app
```

Expected: `spctl` reports notarized/accepted; app launches without Gatekeeper quarantine dialog.

- [ ] **Step 3: No further commit** unless verification finds a bug — then fix and commit with a focused message.

---

## Spec coverage (self-review)

| Spec requirement | Task |
|------------------|------|
| Universal ZIP via lipo | Task 3 |
| `.app` + Developer ID + notarize + staple | Task 2 + 3 |
| Sign only on `release.yml` | Task 3; prerelease untouched |
| Same `APPLE_*` secrets; fail if missing | Task 2 + 3 + 5 |
| Bundle metadata / entitlements / docs | Task 1 |
| `PublishSingleFile=true` on macOS release | Task 3 |
| README update | Task 4 |
| Settings outside sealed bundle | Already true (`AppSettingsService`); documented in Task 1 |

No placeholders left in task steps.
