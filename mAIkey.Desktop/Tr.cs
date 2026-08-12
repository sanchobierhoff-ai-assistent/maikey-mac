using Avalonia;
using Avalonia.Controls;
using mAIkey.Core.Services;

namespace mAIkey.Desktop;

/// <summary>
/// Attached property voor live-vertaling. Gebruik in XAML:
///   &lt;TextBlock loc:Tr.Key="Nav_Dashboard" /&gt;
///   &lt;Button loc:Tr.Key="Common_Save" /&gt;
/// Zet direct de juiste tekst en werkt automatisch bij zodra de taal wisselt (L.Changed).
/// Voor TextBlock wordt Text gezet, voor overige ContentControls de Content.
/// </summary>
public static class Tr
{
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(Tr));

    public static void SetKey(Control c, string? value) => c.SetValue(KeyProperty, value);
    public static string? GetKey(Control c) => c.GetValue(KeyProperty);

    /// <summary>Vertaalt de placeholder/watermark van een TextBox of ComboBox.</summary>
    public static readonly AttachedProperty<string?> WatermarkKeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("WatermarkKey", typeof(Tr));

    public static void SetWatermarkKey(Control c, string? value) => c.SetValue(WatermarkKeyProperty, value);
    public static string? GetWatermarkKey(Control c) => c.GetValue(WatermarkKeyProperty);

    static Tr()
    {
        KeyProperty.Changed.AddClassHandler<Control>((control, e) =>
            Hook(control, e.GetNewValue<string?>(), (c, text) =>
            {
                switch (c)
                {
                    case TextBlock tb: tb.Text = text; break;
                    case ContentControl cc: cc.Content = text; break;
                }
            }));

        WatermarkKeyProperty.Changed.AddClassHandler<Control>((control, e) =>
            Hook(control, e.GetNewValue<string?>(), (c, text) =>
            {
                switch (c)
                {
                    case TextBox tbx: tbx.Watermark = text; break;
                    case ComboBox cb: cb.PlaceholderText = text; break;
                }
            }));
    }

    private static void Hook(Control control, string? key, System.Action<Control, string> apply)
    {
        void Apply()
        {
            if (string.IsNullOrEmpty(key)) return;
            apply(control, L.T(key));
        }

        Apply();

        // Live bijwerken bij taalwissel; opruimen wanneer de control uit beeld gaat.
        System.Action handler = Apply;
        L.Changed += handler;
        void DetachHandler(object? s, VisualTreeAttachmentEventArgs a) => L.Changed -= handler;
        control.DetachedFromVisualTree -= DetachHandler;
        control.DetachedFromVisualTree += DetachHandler;
    }
}
