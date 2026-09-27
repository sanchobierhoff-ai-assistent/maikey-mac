namespace mAIkey.Desktop;

/// <summary>
/// Vertaling met terugval: sommige teksten staan in de Windows-app hard in de XAML (alleen
/// Nederlands). Die gebruiken hier dezelfde tekst als terugval, zodat een ontbrekende sleutel
/// nooit als "[Sleutel]" in beeld komt.
/// </summary>
public static class Loc
{
    public static string T(string key, string fallback)
    {
        var v = L.T(key);
        return v.StartsWith("[") && v.EndsWith("]") ? fallback : v;
    }

    public static string Tf(string key, string fallback, params object[] args)
    {
        try { return string.Format(T(key, fallback), args); }
        catch { return T(key, fallback); }
    }
}
