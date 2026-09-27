// Genereert mAIkey.Desktop/Resources/Colors.axaml uit de Windows-kleurbestanden
// (frontend/Resources/Colors.Light.xaml + Colors.Dark.xaml), zodat beide apps exact
// hetzelfde thema hebben.
const fs = require('fs');
const [,, lightPath, darkPath, outPath] = process.argv;

function body(file) {
  let s = fs.readFileSync(file, 'utf8').replace(/^﻿/, '');
  s = s.replace(/<!--[\s\S]*?-->/g, '');
  s = s.replace(/^[\s\S]*?<ResourceDictionary[^>]*>/, '').replace(/<\/ResourceDictionary>\s*$/, '');
  // thema-onafhankelijke tokens komen apart
  s = s.replace(/\s*<(FontFamily|CornerRadius|Thickness)\b[^>]*>[^<]*<\/\1>/g, '');
  // StaticResource-kleurverwijzingen inline oplossen
  const colors = {};
  for (const m of s.matchAll(/<Color x:Key="([^"]+)">(#[0-9A-Fa-f]+)<\/Color>/g)) colors[m[1]] = m[2];
  s = s.replace(/"\{StaticResource ([A-Za-z0-9]+)\}"/g, (m, k) => colors[k] ? `"${colors[k]}"` : m);
  // WPF relatieve gradiëntpunten → Avalonia procenten
  s = s.replace(/(StartPoint|EndPoint)="([0-9.]+),([0-9.]+)"/g, (m, p, x, y) =>
    `${p}="${Math.round(parseFloat(x) * 100)}%,${Math.round(parseFloat(y) * 100)}%"`);
  const accent = colors['AccentColor'] || '#47906B';
  const dim = colors['AccentDimColor'] || accent;
  s += `
      <!-- FluentTheme-systeemaccent → mAIkey-accent (anders blauw op macOS) -->
      <Color x:Key="SystemAccentColor">${accent}</Color>
      <Color x:Key="SystemAccentColorLight1">${accent}</Color>
      <Color x:Key="SystemAccentColorLight2">${accent}</Color>
      <Color x:Key="SystemAccentColorLight3">${accent}</Color>
      <Color x:Key="SystemAccentColorDark1">${dim}</Color>
      <Color x:Key="SystemAccentColorDark2">${dim}</Color>
      <Color x:Key="SystemAccentColorDark3">${dim}</Color>`;
  return s.split('\n').map(l => l.trim() ? '    ' + l.trimEnd() : '').filter((l, i, a) => l || (a[i - 1] && a[i - 1].trim())).join('\n');
}

const out = `<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ═══════════════════════════════════════════════════════════════ -->
  <!-- mAIkey design-tokens — GEGENEREERD uit frontend/Resources/        -->
  <!-- Colors.Light.xaml + Colors.Dark.xaml (Windows-app). Niet met de   -->
  <!-- hand aanpassen: pas de Windows-bestanden aan en genereer opnieuw  -->
  <!-- (tools/sync-from-windows.sh).                                     -->
  <!-- ═══════════════════════════════════════════════════════════════ -->

  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
${body(lightPath)}
    </ResourceDictionary>

    <ResourceDictionary x:Key="Dark">
${body(darkPath)}
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>

  <!-- ═══════════ Thema-onafhankelijke tokens ═══════════ -->
  <FontFamily x:Key="FontUi">avares://mAIkey.Desktop/Resources/Fonts#Geist, Helvetica Neue, Arial, sans-serif</FontFamily>
  <FontFamily x:Key="FontMono">avares://mAIkey.Desktop/Resources/Fonts#JetBrains Mono, Menlo, Courier New, monospace</FontFamily>

  <CornerRadius x:Key="RadiusSm">6</CornerRadius>
  <CornerRadius x:Key="RadiusMd">10</CornerRadius>
  <CornerRadius x:Key="RadiusLg">14</CornerRadius>
  <CornerRadius x:Key="RadiusXl">18</CornerRadius>
  <CornerRadius x:Key="CornerRadiusStandard">10</CornerRadius>
  <CornerRadius x:Key="CornerRadiusCard">14</CornerRadius>
  <CornerRadius x:Key="CornerRadiusPill">25</CornerRadius>
  <CornerRadius x:Key="CornerRadiusWindow">18</CornerRadius>

  <Thickness x:Key="PaddingStandard">12</Thickness>
  <Thickness x:Key="PaddingCard">16</Thickness>
  <Thickness x:Key="PaddingLarge">24</Thickness>
</ResourceDictionary>
`;
fs.writeFileSync(outPath, out);
console.log('Colors.axaml gegenereerd');
