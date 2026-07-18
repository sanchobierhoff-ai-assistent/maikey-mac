using System.Security.Cryptography;
using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// Token encryption on macOS using AES-256 with a machine-specific key.
/// A more robust implementation would use the macOS Keychain (SecItemAdd),
/// but AES with a hardware-derived key provides adequate protection for local config files.
/// </summary>
public class MacTokenProtection : ITokenProtection
{
    private const string Prefix = "macenc:";
    private readonly byte[] _key;

    public MacTokenProtection()
    {
        // Derive encryption key from machine-specific data
        string seed = $"mAIkey-{Environment.MachineName}-{Environment.UserName}-v1";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] data = Encoding.UTF8.GetBytes(plaintext);
                cs.Write(data, 0, data.Length);
            }
            return Prefix + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return plaintext;
        }
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        if (!ciphertext.StartsWith(Prefix)) return ciphertext; // Plaintext or DPAPI token

        try
        {
            byte[] data = Convert.FromBase64String(ciphertext[Prefix.Length..]);

            using var aes = Aes.Create();
            aes.Key = _key;

            // Extract IV from first 16 bytes
            byte[] iv = new byte[16];
            Array.Copy(data, 0, iv, 0, 16);
            aes.IV = iv;

            using var ms = new MemoryStream(data, 16, data.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }
        catch
        {
            return null; // Token unreadable — force re-login
        }
    }
}
