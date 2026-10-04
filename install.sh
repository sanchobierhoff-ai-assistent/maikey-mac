#!/bin/bash
# ======================================================
#  mAIkey voor Mac — installeren met één commando:
#
#    curl -fsSL https://raw.githubusercontent.com/sanchobierhoff-ai-assistent/maikey-mac/main/install.sh | bash
#
#  Wat het doet:
#   1. kiest de juiste versie (Apple Silicon of Intel);
#   2. downloadt de nieuwste mAIkey van GitHub en zet hem in /Applications
#      (of ~/Applications als je geen beheerder bent);
#   3. start mAIkey.
#  Bestanden die met curl worden gedownload krijgen geen "quarantaine"-vlag, dus
#  macOS vraagt niet om de app via rechtsklik → Open toe te staan. Daarna werkt
#  mAIkey zichzelf automatisch bij.
# ======================================================
set -euo pipefail

REPO="sanchobierhoff-ai-assistent/maikey-mac"
APP_NAME="mAIkey.app"

say()  { printf '\033[1;32m▸\033[0m %s\n' "$1"; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

[ "$(uname -s)" = "Darwin" ] || fail "Dit script is alleen voor macOS."

case "$(uname -m)" in
  arm64)  RID="osx-arm64" ;;
  x86_64)
    # Een Intel-build onder Rosetta op een Apple-Silicon-Mac? Dan toch de arm64-versie.
    if [ "$(sysctl -in sysctl.proc_translated 2>/dev/null || echo 0)" = "1" ]; then RID="osx-arm64"; else RID="osx-x64"; fi ;;
  *) fail "Onbekende processor: $(uname -m)" ;;
esac
say "Processor: $RID"

# Nieuwste release voor dit kanaal zoeken (tags heten bv. osx-arm64-1.0.42).
say "Nieuwste versie opzoeken…"
TAG="$(curl -fsSL "https://api.github.com/repos/$REPO/releases?per_page=30" \
  | grep -o "\"tag_name\": *\"$RID-[0-9.]*\"" | head -n1 | sed 's/.*"\([^"]*\)"$/\1/')" || true
[ -n "${TAG:-}" ] || fail "Kon geen release vinden voor $RID. Probeer het later opnieuw."
VERSION="${TAG#$RID-}"
say "Versie $VERSION gevonden"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
ZIP_URL="https://github.com/$REPO/releases/download/$TAG/mAIkey-$RID-Portable.zip"
say "Downloaden…"
curl -fL --progress-bar "$ZIP_URL" -o "$TMP/mAIkey.zip" || fail "Download mislukt ($ZIP_URL)."

say "Uitpakken…"
ditto -x -k "$TMP/mAIkey.zip" "$TMP/unzipped"
SRC="$(find "$TMP/unzipped" -maxdepth 2 -name '*.app' -type d | head -n1)"
[ -n "$SRC" ] || fail "Geen .app gevonden in de download."

# Doelmap: /Applications als die schrijfbaar is, anders ~/Applications.
DEST_DIR="/Applications"
if [ ! -w "$DEST_DIR" ]; then
  DEST_DIR="$HOME/Applications"
  mkdir -p "$DEST_DIR"
fi
DEST="$DEST_DIR/$APP_NAME"

# Een draaiende mAIkey eerst netjes afsluiten.
if pgrep -f "$APP_NAME/Contents/MacOS" >/dev/null 2>&1; then
  say "mAIkey afsluiten…"
  osascript -e 'quit app "mAIkey"' >/dev/null 2>&1 || true
  sleep 2
  pkill -f "$APP_NAME/Contents/MacOS" >/dev/null 2>&1 || true
fi

say "Installeren in ${DEST_DIR}…"
rm -rf "$DEST"
ditto "$SRC" "$DEST"
# Voor de zekerheid: eventuele quarantaine-vlag weghalen (bv. bij een proxy/AV die hem zet).
xattr -dr com.apple.quarantine "$DEST" 2>/dev/null || true

say "mAIkey starten…"
open "$DEST"

cat <<'EOF'

✅ mAIkey is geïnstalleerd.

Nog één ding: om geselecteerde tekst te kunnen lezen en het resultaat terug te
plakken vraagt macOS om toestemming voor "Toegankelijkheid". mAIkey laat je dat
zien; zet mAIkey daar aan (Systeeminstellingen → Privacy en beveiliging →
Toegankelijkheid). Dit hoeft maar één keer — ook na updates.

EOF
