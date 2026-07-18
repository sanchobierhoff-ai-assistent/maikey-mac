namespace mAIkey.Core.Interfaces;

/// <summary>
/// Encrypts/decrypts authentication tokens for secure local storage.
/// Windows: DPAPI (ProtectedData)
/// macOS: Keychain (SecItemAdd/SecItemCopyMatching)
/// </summary>
public interface ITokenProtection
{
    /// <summary>
    /// Encrypt a plaintext token for secure storage.
    /// </summary>
    string? Encrypt(string? plaintext);

    /// <summary>
    /// Decrypt a previously encrypted token.
    /// Returns null if decryption fails (different user/machine).
    /// </summary>
    string? Decrypt(string? ciphertext);
}
