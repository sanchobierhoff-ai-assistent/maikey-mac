# mAIkey — macOS

De macOS-versie van mAIkey (Avalonia). Deelt de kernlogica (`mAIkey.Core`) en is
qua UI gelijkgetrokken met de Windows-app (WPF).

## Bouwen op een Mac

Vereist [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
# Apple Silicon (M1/M2/M3):
dotnet publish mAIkey.Desktop/mAIkey.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o publish
codesign --force --deep --sign - publish/mAIkey.Desktop   # ad-hoc, gratis
./publish/mAIkey.Desktop
```

Voor Intel-Macs: vervang `osx-arm64` door `osx-x64`.

## Cloud-build

Bij elke push bouwt GitHub Actions automatisch een `.app` (zie de **Actions**-tab,
workflow *Build macOS app*). Download het artifact, pak uit, en open de eerste keer
met rechtsklik → *Open*.

## Release met auto-update

`./BUILD_RELEASE_MAC.sh <versie>` publiceert een Velopack-release (kanaal `osx`) naar
de releases-repo. Vereist een Apple Developer ID voor notarisatie zodat auto-updates
door Gatekeeper komen.