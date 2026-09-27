// Mechanische eerste stap: WPF code-behind → Avalonia. Resultaat altijd handmatig nalopen.
// Gebruik: node cs2ava.js <in.cs> <out.cs> <namespace>
const fs = require('fs');
const [,, inFile, outFile, ns] = process.argv;
let s = fs.readFileSync(inFile, 'utf8').replace(/\r\n/g, '\n');

// usings
s = s.replace(/^using System\.Windows(\.[A-Za-z.]+)?;\n/gm, '');
s = s.replace(/^using ai_assistant_wpf[A-Za-z.]*;\n/gm, '');
s = s.replace(/^using System\.Windows\.Threading;\n/gm, '');
const usings = [
  'using Avalonia;', 'using Avalonia.Controls;', 'using Avalonia.Input;', 'using Avalonia.Interactivity;',
  'using Avalonia.Layout;', 'using Avalonia.Media;', 'using Avalonia.Threading;', 'using mAIkey.Desktop.Controls;',
  'using mAIkey.Desktop.Windows;', 'using mAIkey.Desktop.Services;'
].join('\n') + '\n';
s = s.replace(/(using [^\n]+;\n)(?!using)/, `$1${usings}`);

// namespace → file-scoped
s = s.replace(/namespace ai_assistant_wpf(\.[A-Za-z]+)?\s*\n\{\n/, `namespace ${ns};\n\n`);
s = s.replace(/\n\}\s*$/, '\n');
// de-indent 4 spaces
s = s.split('\n').map(l => l.startsWith('    ') ? l.slice(4) : l).join('\n');

// Visibility
s = s.replace(/\.Visibility\s*=\s*([^;]+?)\s*\?\s*Visibility\.Visible\s*:\s*Visibility\.Collapsed;/g, '.IsVisible = $1;');
s = s.replace(/\.Visibility\s*=\s*([^;]+?)\s*\?\s*Visibility\.Collapsed\s*:\s*Visibility\.Visible;/g, '.IsVisible = !($1);');
s = s.replace(/\.Visibility\s*=\s*Visibility\.Visible/g, '.IsVisible = true');
s = s.replace(/\.Visibility\s*=\s*Visibility\.(Collapsed|Hidden)/g, '.IsVisible = false');
s = s.replace(/(\w+)\.Visibility\s*!=\s*Visibility\.Visible/g, '!$1.IsVisible');
s = s.replace(/(\w+)\.Visibility\s*==\s*Visibility\.Visible/g, '$1.IsVisible');
s = s.replace(/(\w+)\.Visibility\s*==\s*Visibility\.Collapsed/g, '!$1.IsVisible');
s = s.replace(/Visibility = Visibility\.Collapsed/g, 'IsVisible = false');
s = s.replace(/Visibility = Visibility\.Visible/g, 'IsVisible = true');

// resources / styles
s = s.replace(/\(Brush\)FindResource\(("[^"]+")\)/g, 'Ui.Brush($1)');
s = s.replace(/\(SolidColorBrush\)FindResource\(("[^"]+")\)/g, 'Ui.Brush($1)');
s = s.replace(/\(Brush\)Application\.Current\.FindResource\(("[^"]+")\)/g, 'Ui.Brush($1)');
s = s.replace(/Style = \(Style\)FindResource\(("[^"]+")\)/g, 'Classes = { $1 }');
s = s.replace(/(\w+)\.Style = \(Style\)FindResource\(([^;]+)\);/g, 'Ui.SetKind($1, $2);');
s = s.replace(/Cursor = Cursors\.Hand/g, 'Cursor = new Cursor(StandardCursorType.Hand)');
s = s.replace(/FontStyles\.Italic/g, 'FontStyle.Italic');
s = s.replace(/\bBrush\b(?=\s+\w+\s*[=(;])/g, 'IBrush');
s = s.replace(/new SolidColorBrush\(Color\.FromRgb/g, 'new SolidColorBrush(Color.FromRgb');
s = s.replace(/ToolTip = ("[^"]*")/g, '/* tip */ Tag2 = $1');

// events / args
s = s.replace(/\(object sender, /g, '(object? sender, ');
s = s.replace(/RoutedEventHandler /g, 'EventHandler<RoutedEventArgs> ');
s = s.replace(/MouseButtonEventArgs/g, 'PointerReleasedEventArgs');
s = s.replace(/\.MouseLeftButtonUp \+=/g, '.PointerReleased +=');
s = s.replace(/Loaded \+= /g, 'Opened += ');
s = s.replace(/\(Keyboard\.Modifiers & ModifierKeys\.Shift\) == 0/g, '!e.KeyModifiers.HasFlag(KeyModifiers.Shift)');
s = s.replace(/\(Keyboard\.Modifiers & ModifierKeys\.Shift\) != 0/g, 'e.KeyModifiers.HasFlag(KeyModifiers.Shift)');
s = s.replace(/Keyboard\.Modifiers\.HasFlag\(ModifierKeys\.Shift\)/g, 'e.KeyModifiers.HasFlag(KeyModifiers.Shift)');
s = s.replace(/Key\.Return/g, 'Key.Enter');
s = s.replace(/Dispatcher\.BeginInvoke\(new Action\(\(\) => ([^)]*\(\))\)\)/g, 'Dispatcher.UIThread.Post(() => $1)');
s = s.replace(/Dispatcher\.(Begin)?Invoke\(/g, 'Dispatcher.UIThread.Post(');
s = s.replace(/(\w+)\.Clear\(\);/g, (m, n) => /Box$|Input$|TextBox$/.test(n) ? `${n}.Text = "";` : m);
s = s.replace(/FrameworkElement/g, 'Control');
s = s.replace(/Window\.GetWindow\(this\)/g, '(TopLevel.GetTopLevel(this) as Window)');
s = s.replace(/\.Text\.Trim\(\)/g, '.Text?.Trim() ?? ""');
s = s.replace(/L\.T\(/g, 'L.T(');

// MkDialog: synchrone WPF-dialogen zijn async op de Mac
s = s.replace(/(?<!await )MkDialog\.Show(Info|Error)\(/g, 'await MkDialog.Show$1(');
s = s.replace(/(?<!await )MkDialog\.Show(Confirm|Upgrade)\(/g, 'await MkDialog.Show$1(');

fs.writeFileSync(outFile, s);
console.log('ok', outFile);
