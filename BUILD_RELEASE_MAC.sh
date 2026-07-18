#!/usr/bin/env bash
# ======================================================
#  mAIkey — BUILD RELEASE (macOS)
# ======================================================
#  Tegenhanger van BUILD_RELEASE.bat, maar voor de Mac-app (Avalonia,
#  project mAIkey.Desktop). Draait OP een Mac. Doet:
#    publish (osx) -> vpk pack (.app + delta) -> [sign + notarize]
#    -> upload naar dezelfde GitHub-releases-repo, kanaal "osx".
#
#  De Mac-app haalt updates uit hetzelfde feed via kanaal "osx", dus
#  gebruikers installeren éénmalig en de app update zichzelf.
#
#  Gebruik:
#    ./BUILD_RELEASE_MAC.sh 1.5.0              # echte release
#    ./BUILD_RELEASE_MAC.sh 1.5.0 --dryrun     # alles lokaal, niets online
#
#  Vereist (eenmalig op de Mac):
#    - .NET SDK 8 (voor vpk):  https://dotnet.microsoft.com/download
#    - vpk tool:               dotnet tool install -g vpk --version 1.2.0
#    - Voor signing/notarize:  Apple Developer ID + notarytool-profiel
# ======================================================
set -euo pipefail
cd "$(dirname "$0")"

# ── Config ──
REPO_URL="https://github.com/sanchobierhoff-ai-assistent/ai-assistent"
PROJECT="mAIkey.Desktop/mAIkey.Desktop.csproj"
PACK_ID="mAIkey"
MAIN_EXE="mAIkey.Desktop"          # naam van het binary in de publish-map
CHANNEL="osx"
PUBDIR="vpk_work/publish-osx"
RELDIR="Releases-osx"
# Architectuur: osx-arm64 (Apple Silicon) of osx-x64 (Intel)
RID="${RID:-osx-arm64}"
# Optioneel icoon (.icns). Laat leeg om het standaard-icoon te gebruiken.
ICNS="${ICNS:-}"

# ── Apple signing/notarisatie (optioneel maar aanbevolen) ──
# Vul in om Gatekeeper-waarschuwingen te vermijden en auto-update te laten werken:
#   SIGN_IDENTITY="Developer ID Application: Jouw Naam (TEAMID)"
#   NOTARY_PROFILE="notarytool-profielnaam"   (aangemaakt met: xcrun notarytool store-credentials)
SIGN_IDENTITY="${SIGN_IDENTITY:-}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"

# ── Argumenten ──
VERSION="${1:-}"
DRYRUN=0
[ "${2:-}" = "--dryrun" ] && DRYRUN=1
if [ -z "$VERSION" ]; then
  echo "Gebruik: ./BUILD_RELEASE_MAC.sh <versie> [--dryrun]   (bijv. 1.5.0)"; exit 1
fi

echo "======================================================"
if [ "$DRYRUN" = "1" ]; then echo "  mAIkey — BUILD RELEASE macOS  [ DRY-RUN ]"; else echo "  mAIkey — BUILD RELEASE macOS  [ ECHTE RELEASE ]"; fi
echo "  versie $VERSION  |  rid $RID  |  kanaal $CHANNEL"
echo "======================================================"

command -v vpk >/dev/null 2>&1 || { echo "[FOUT] 'vpk' niet gevonden. Installeer: dotnet tool install -g vpk --version 1.2.0"; exit 1; }

# ── [1/5] Publish (self-contained) ──
echo "[1/5] Publish ($RID)..."
rm -rf "$PUBDIR"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true -o "$PUBDIR" -nologo
[ -f "$PUBDIR/$MAIN_EXE" ] || { echo "[FOUT] $MAIN_EXE ontbreekt in $PUBDIR"; exit 1; }

# ── [2/5] Vorige release ophalen (voor delta) ──
echo "[2/5] Vorige release ophalen (delta)..."
rm -rf "$RELDIR"; mkdir -p "$RELDIR"
vpk download github --repoUrl "$REPO_URL" --channel "$CHANNEL" --outputDir "$RELDIR" 2>/dev/null || echo "  (geen vorige release — eerste osx-release)"

# ── [3/5] Pack (.app + delta) ──
echo "[3/5] Pack..."
PACK_ARGS=(pack
  --packId "$PACK_ID"
  --packVersion "$VERSION"
  --packDir "$PUBDIR"
  --mainExe "$MAIN_EXE"
  --packTitle "mAIkey"
  --packAuthors "mAIkey"
  --channel "$CHANNEL"
  --outputDir "$RELDIR")
[ -n "$ICNS" ] && PACK_ARGS+=(--icon "$ICNS")
[ -n "$SIGN_IDENTITY" ] && PACK_ARGS+=(--signAppIdentity "$SIGN_IDENTITY")
[ -n "$NOTARY_PROFILE" ] && PACK_ARGS+=(--notaryProfile "$NOTARY_PROFILE")
vpk "${PACK_ARGS[@]}"
echo "  -> .app + delta gemaakt in $RELDIR"

if [ "$DRYRUN" = "1" ]; then
  echo "======================================================"
  echo "  DRY-RUN klaar — niets online. Bekijk: $RELDIR"
  echo "======================================================"
  exit 0
fi

# ── [4/5] GitHub-token ──
echo "[4/5] GitHub-token..."
: "${GH_TOKEN:?Zet eerst GH_TOKEN (export GH_TOKEN=ghp_...) met release-rechten op de repo}"

# ── [5/5] Upload + publiceren ──
echo "[5/5] Upload naar GitHub (kanaal $CHANNEL)..."
vpk upload github \
  --outputDir "$RELDIR" \
  --repoUrl "$REPO_URL" \
  --channel "$CHANNEL" \
  --token "$GH_TOKEN" \
  --publish true --merge true \
  --releaseName "mAIkey (macOS) $VERSION" \
  --tag "osx-v$VERSION"

echo "======================================================"
echo "  RELEASE COMPLEET (macOS) — $VERSION"
echo "  $REPO_URL/releases/tag/osx-v$VERSION"
echo "======================================================"
