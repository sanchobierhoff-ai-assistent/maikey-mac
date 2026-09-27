// Ruwe WPF-XAML → Avalonia-XAML-conversie voor de mAIkey-port. Output is een startpunt,
// geen eindproduct: markeert onvertaalbare constructies met <!-- TODO-PORT -->.
const fs = require('fs');
const path = require('path');

const [,, src, dst, ns = 'mAIkey.Desktop.Views'] = process.argv;
let s = fs.readFileSync(src, 'utf8').replace(/^﻿/, '');

const kebab = k => k.replace(/([a-z0-9])([A-Z])/g, '$1-$2').replace(/([A-Z])([A-Z][a-z])/g, '$1-$2').toLowerCase();

// Root/namespaces
s = s.replace(/x:Class="ai_assistant_wpf\.(Views\.)?([A-Za-z0-9_]+)"/, (_, v, n) => `x:Class="${ns}.${n}"`);
s = s.replace(/xmlns="http:\/\/schemas\.microsoft\.com\/winfx\/2006\/xaml\/presentation"/, 'xmlns="https://github.com/avaloniaui"');
s = s.replace(/\s+xmlns:materialDesign="[^"]*"/g, '\n             xmlns:i="https://github.com/projektanker/icons.avalonia"');
s = s.replace(/\s+xmlns:(d|mc|local|controls|svc|conv|sys)="[^"]*"/g, '');
s = s.replace(/\s+mc:Ignorable="[^"]*"/g, '');
s = s.replace(/\s+d:Design(Height|Width)="[^"]*"/g, '');

// Resources-blokken (merged dictionaries/converters/storyboards) weg
s = s.replace(/<(UserControl|Window)\.Resources>[\s\S]*?<\/\1\.Resources>/g, '<!-- TODO-PORT: resources verwijderd -->');
s = s.replace(/<([A-Za-z]+)\.Resources>[\s\S]*?<\/\1\.Resources>/g, '');
s = s.replace(/<([A-Za-z]+)\.Effect>[\s\S]*?<\/\1\.Effect>/g, '');
s = s.replace(/<([A-Za-z]+)\.Clip>[\s\S]*?<\/\1\.Clip>/g, '');
s = s.replace(/<([A-Za-z]+)\.Triggers>[\s\S]*?<\/\1\.Triggers>/g, '<!-- TODO-PORT: triggers -->');
s = s.replace(/<([A-Za-z]+)\.Template>[\s\S]*?<\/\1\.Template>/g, '<!-- TODO-PORT: template -->');
s = s.replace(/<([A-Za-z]+)\.Style>[\s\S]*?<\/\1\.Style>/g, '<!-- TODO-PORT: style -->');

// PackIcon → Icon
s = s.replace(/<materialDesign:PackIcon\b([^>]*?)\/>/g, (m, attrs) => {
  let kind = (attrs.match(/Kind="([^"]+)"/) || [])[1] || 'Help';
  let w = (attrs.match(/\bWidth="([^"]+)"/) || [])[1];
  attrs = attrs.replace(/\s*Kind="[^"]+"/, '').replace(/\s*\bWidth="[^"]+"/, '').replace(/\s*\bHeight="[^"]+"/, '');
  return `<i:Icon Value="mdi-${kebab(kind)}"${w ? ` FontSize="${w}"` : ''}${attrs}/>`;
});
// PackIcon met kind-elementen (bv. <PackIcon.Foreground>)
s = s.replace(/<materialDesign:PackIcon\b([^>]*?)>/g, (m, attrs) => {
  let kind = (attrs.match(/Kind="([^"]+)"/) || [])[1] || 'Help';
  let w = (attrs.match(/\bWidth="([^"]+)"/) || [])[1];
  attrs = attrs.replace(/\s*Kind="[^"]+"/, '').replace(/\s*\bWidth="[^"]+"/, '').replace(/\s*\bHeight="[^"]+"/, '');
  return `<i:Icon Value="mdi-${kebab(kind)}"${w ? ` FontSize="${w}"` : ''}${attrs}>`;
});
s = s.replace(/<\/materialDesign:PackIcon>/g, '</i:Icon>').replace(/materialDesign:PackIcon\./g, 'i:Icon.');

// Gradiënt-punten: WPF relatief (0..1) → Avalonia procenten
s = s.replace(/(StartPoint|EndPoint)="([0-9.]+),([0-9.]+)"/g, (m, p, x, y) => `${p}="${Math.round(parseFloat(x)*100)}%,${Math.round(parseFloat(y)*100)}%"`);
s = s.replace(/StrokeStartLineCap="([A-Za-z]+)"/g, 'StrokeLineCap="$1"').replace(/\s+StrokeEndLineCap="[A-Za-z]+"/g, "");

// Stijlen → classes
s = s.replace(/Style="\{(?:StaticResource|DynamicResource) ([A-Za-z0-9_]+)\}"/g, 'Classes="$1"');
s = s.replace(/\{StaticResource /g, '{DynamicResource ');

// Zichtbaarheid
s = s.replace(/Visibility="Collapsed"/g, 'IsVisible="False"');
s = s.replace(/\s*Visibility="Visible"/g, '');
s = s.replace(/Visibility="Hidden"/g, 'Opacity="0"');

// Events
const ev = { MouseLeftButtonDown: 'PointerPressed', MouseLeftButtonUp: 'PointerReleased', MouseEnter: 'PointerEntered',
  MouseLeave: 'PointerExited', PreviewKeyDown: 'KeyDown', PreviewMouseLeftButtonDown: 'PointerPressed',
  MouseDown: 'PointerPressed', MouseUp: 'PointerReleased', PreviewTextInput: 'TextInput' };
for (const [w, a] of Object.entries(ev)) s = s.replace(new RegExp(`\\b${w}="`, 'g'), `${a}="`);
s = s.replace(/\bChecked="([A-Za-z0-9_]+)"\s+Unchecked="\1"/g, 'IsCheckedChanged="$1"');
s = s.replace(/\bUnchecked="([A-Za-z0-9_]+)"\s+Checked="\1"/g, 'IsCheckedChanged="$1"');
s = s.replace(/\bChecked="/g, 'IsCheckedChanged="').replace(/\s+Unchecked="[^"]*"/g, '');

// Attributen
s = s.replace(/\bToolTip="/g, 'ToolTip.Tip="');
s = s.replace(/materialDesign:HintAssist\.Hint="/g, 'Watermark="');
s = s.replace(/\s+materialDesign:[A-Za-z.]+="[^"]*"/g, '');
s = s.replace(/DisplayMemberPath="([^"]+)"/g, 'DisplayMemberBinding="{Binding $1}"');
s = s.replace(/SelectedValuePath="([^"]+)"/g, 'SelectedValueBinding="{Binding $1}"');
s = s.replace(/\s+(SnapsToDevicePixels|UseLayoutRounding|RenderOptions\.[A-Za-z]+|CanContentScroll|PanningMode|TextOptions\.[A-Za-z]+|ScrollViewer\.CanContentScroll|VirtualizingPanel\.[A-Za-z]+|VirtualizingStackPanel\.[A-Za-z]+|OverridesDefaultStyle|FocusVisualStyle|KeyboardNavigation\.[A-Za-z]+|LineStackingStrategy|IsDeferredScrollingEnabled|AllowsTransparency|WindowStyle|ResizeMode|ShowActivated)="[^"]*"/g, '');
s = s.replace(/FontFamily="[^"]*(Consolas|Cascadia)[^"]*"/g, 'FontFamily="{DynamicResource FontMono}"');
s = s.replace(/<ListView\b/g, '<ListBox').replace(/<\/ListView>/g, '</ListBox>').replace(/ListView\./g, 'ListBox.');
s = s.replace(/<Hyperlink\b/g, '<!-- TODO-PORT: Hyperlink --><Span').replace(/<\/Hyperlink>/g, '</Span>');
s = s.replace(/\bVerticalScrollBarVisibility="(Auto|Visible|Hidden|Disabled)"/g, 'ScrollViewer.VerticalScrollBarVisibility="$1"')
     .replace(/<ScrollViewer([^>]*?)ScrollViewer\.VerticalScrollBarVisibility=/g, '<ScrollViewer$1VerticalScrollBarVisibility=');
s = s.replace(/\bHorizontalScrollBarVisibility="(Auto|Visible|Hidden|Disabled)"/g, 'ScrollViewer.HorizontalScrollBarVisibility="$1"')
     .replace(/<ScrollViewer([^>]*?)ScrollViewer\.HorizontalScrollBarVisibility=/g, '<ScrollViewer$1HorizontalScrollBarVisibility=');

// Window-attributen die Avalonia anders noemt
s = s.replace(/WindowStartupLocation="CenterOwner"/g, 'WindowStartupLocation="CenterOwner"');

fs.mkdirSync(path.dirname(dst), { recursive: true });
fs.writeFileSync(dst, s);
const todos = (s.match(/TODO-PORT/g) || []).length;
console.log(`${path.basename(src)} -> ${path.basename(dst)}  (${todos} TODO)`);
