using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GreekSwitch.App.Policy;

/// <summary>The real policy keys: HKLM and HKCU \Software\Policies\GreekSwitch, read-only.</summary>
internal sealed class RegistryPolicy(ILogger logger) : IPolicyRegistry
{
    public const string KeyPath = @"Software\Policies\GreekSwitch";

    public object? GetValue(PolicyScope scope, string name)
    {
        var root = scope == PolicyScope.Machine ? Registry.LocalMachine : Registry.CurrentUser;
        try
        {
            using var key = root.OpenSubKey(KeyPath, writable: false);
            return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            logger.LogWarning("Policy value {Name} ({Scope}) could not be read ({Type})", name, scope, ex.GetType().Name);
            return null;
        }
    }
}
