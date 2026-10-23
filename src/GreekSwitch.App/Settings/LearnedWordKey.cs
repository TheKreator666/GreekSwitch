using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.Settings;

/// <summary>
/// The secret HMAC key behind the hashed never-convert words. It lives in settings.json encrypted
/// with DPAPI for the current Windows user, so a copied settings file (another machine, another
/// account, a backup) can't be used to test guesses against the hashes.
/// </summary>
internal static class LearnedWordKey
{
    private static readonly byte[] Entropy = "GreekSwitch.NeverConvert.v1"u8.ToArray();

    /// <summary>Returns the key, creating (and storing in <paramref name="settings"/>) a new one if needed.</summary>
    /// <returns>The key, and whether <paramref name="settings"/> changed and should be saved.</returns>
    public static (byte[] Key, bool SettingsChanged) LoadOrCreate(AppSettings settings, ILogger logger)
    {
        if (settings.NeverConvertKey is { } stored)
        {
            try
            {
                return (ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser), false);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // E.g. settings.json copied from another machine or user: those hashes are unusable here.
                logger.LogWarning("Learned-word key could not be decrypted ({Type}); starting fresh", ex.GetType().Name);
                settings.NeverConvertLearned = [];
            }
        }

        var key = RandomNumberGenerator.GetBytes(32);
        settings.NeverConvertKey = Convert.ToBase64String(ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
        return (key, true);
    }
}
