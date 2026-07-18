using System.Security.Cryptography;
using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Token encryption using Windows DPAPI (ProtectedData).
/// Ported from frontend/Services/ConfigService.cs.
/// </summary>
public class WindowsTokenProtection : ITokenProtection
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("mAIkey-token-v1");

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        try
        {
            var data = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(encrypted);
        }
        catch
        {
            return plaintext;
        }
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        if (!ciphertext.StartsWith(Prefix)) return ciphertext;
        try
        {
            var data = Convert.FromBase64String(ciphertext[Prefix.Length..]);
            var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }
}
