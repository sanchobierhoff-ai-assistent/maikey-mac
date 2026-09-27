# mAIkey — macOS

De macOS-versie van mAIkey (Avalonia). Functioneel en qua uiterlijk gelijk aan de
Windows-app (WPF): dezelfde schermen, dezelfde API-client en dezelfde configuratie-indeling
(sneltoetsen zijn uitwisselbaar via Cloud Sync).

## Installeren (gratis, één commando)

Open **Terminal** en plak:

```bash
curl -fsSL https://raw.githubusercontent.com/sanchobierhoff-ai-assistent/maikey-mac/main/install.sh | bash
```

Het script kiest zelf Apple Silicon of Intel, zet mAIkey in *Programma's* en start hem.
Omdat het via Terminal gaat, hoef je Gatekeeper niet via rechtsklik → *Open* toe te staan.
Bij de eerste start vraagt macOS om **Toegankelijkheid** (nodig om geselecteerde tekst te
kopiëren en het resultaat terug te plakken). Daarna werkt mAIkey zichzelf automatisch bij.

## Releases

Elke push naar `main` die `mAIkey.Desktop/` of `mAIkey.Core/` raakt, laat GitHub Actions
(`.github/workflows/build-mac.yml`, gratis macOS-runner) een release bouwen voor
`osx-arm64` en `osx-x64`. De app haalt updates uit die releases (Velopack).

### Vast certificaat (eenmalig instellen)

Zonder Apple Developer-account ondertekent de build met een eigen, gratis certificaat.
Dat zorgt dat macOS een update als dezelfde app herkent, zodat de Toegankelijkheid-
toestemming blijft staan. Eenmalig:

1. `tools/make-signing-cert.sh` (Git Bash of Terminal) — maakt het certificaat buiten de repo.
2. GitHub → *Settings* → *Secrets and variables* → *Actions*:
   `MAC_CERT_P12` = inhoud van `mac-cert.p12.base64`,
   `MAC_CERT_PASSWORD` = inhoud van `mac-cert.password`.

Zonder deze secrets valt de build terug op ad-hoc-ondertekening (werkt ook, maar dan moet
de toestemming na elke update opnieuw).

## Bouwen op een Mac

Vereist [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet publish mAIkey.Desktop/mAIkey.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o publish
codesign --force --deep --sign - publish/mAIkey.Desktop
./publish/mAIkey.Desktop
```

Compileren kan ook op Windows (alleen controleren, niet draaien):

```bash
dotnet build mAIkey.Desktop/mAIkey.Desktop.csproj -r osx-arm64 --self-contained false
```

In een Debug-build op Windows kun je alle schermen laten renderen naar PNG's
(gebruikt een eigen map `%LOCALAPPDATA%\mAIkey-mac-dev`, nooit de config van de Windows-app):

```bash
MAIKEY_UI_PREVIEW=1 MAIKEY_UI_SNAPSHOT=C:\temp\snap ./mAIkey.Desktop.exe
```

## Gelijk houden met Windows

- `mAIkey.Core/Shared/` is een kopie van de gedeelde Windows-code (ApiClient, ConfigService,
  modellen) met alleen de namespace aangepast. Bij wijzigingen in de Windows-app deze
  bestanden opnieuw kopiëren.
- `tools/port/` bevat de hulpscripts waarmee de schermen zijn overgezet:
  `wpf2ava.js` (WPF-XAML → Avalonia), `cs2ava.js` (code-behind, eerste aanzet),
  `colors2ava.js` (thema-kleuren) en `lockeys.js` (controleert of alle vertaalsleutels
  in nl/en/de bestaan).
