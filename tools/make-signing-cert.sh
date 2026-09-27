#!/bin/bash
# ======================================================
#  Maakt het gratis, zelfondertekende code-signing-certificaat voor de Mac-build.
#
#  Waarom: zonder vast certificaat ondertekent de build "ad-hoc". macOS ziet dan elke update
#  als een nieuwe app en vergeet de Toegankelijkheid-toestemming. Met één vast certificaat
#  (ook al is het niet van Apple) blijft die toestemming na updates gewoon staan.
#
#  Gebruik (Git Bash op Windows of Terminal op een Mac):
#    tools/make-signing-cert.sh [uitvoermap]
#  Daarna in GitHub → repo maikey-mac → Settings → Secrets and variables → Actions:
#    MAC_CERT_P12      = inhoud van  mac-cert.p12.base64
#    MAC_CERT_PASSWORD = inhoud van  mac-cert.password
#  Bewaar de map goed (niet in git): met een nieuw certificaat moeten gebruikers de
#  toestemming nog één keer opnieuw geven.
# ======================================================
set -euo pipefail

OUT="${1:-$HOME/maikey-mac-signing}"
NAME="mAIkey Self-Signed"
mkdir -p "$OUT"
cd "$OUT"

if [ -f mac-cert.p12 ]; then
  echo "Er staat al een certificaat in $OUT — niets gedaan (verwijder het eerst als je echt een nieuw wilt)."
  exit 1
fi

PASSWORD="$(openssl rand -hex 24)"

cat > cert.cnf <<EOF
[req]
distinguished_name = dn
prompt = no
x509_extensions = ext
[dn]
CN = $NAME
O = mAIkey
[ext]
basicConstraints = critical, CA:false
keyUsage = critical, digitalSignature
extendedKeyUsage = critical, codeSigning
EOF

# 10 jaar geldig; een verlopen certificaat breekt bestaande installaties niet, maar nieuwe
# builds moeten dan een nieuw certificaat krijgen.
openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -config cert.cnf \
  -keyout mac-cert.key -out mac-cert.pem 2>/dev/null

# macOS' `security import` kan de nieuwe OpenSSL-3-versleuteling niet lezen → klassieke algoritmes.
openssl pkcs12 -export -inkey mac-cert.key -in mac-cert.pem -name "$NAME" \
  -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1 \
  -passout "pass:$PASSWORD" -out mac-cert.p12

base64 < mac-cert.p12 | tr -d '\n' > mac-cert.p12.base64
printf '%s' "$PASSWORD" > mac-cert.password
rm -f mac-cert.key cert.cnf

echo "Klaar. Bestanden in: $OUT"
echo "  mac-cert.p12.base64  → secret MAC_CERT_P12"
echo "  mac-cert.password    → secret MAC_CERT_PASSWORD"
openssl x509 -in mac-cert.pem -noout -subject -enddate
