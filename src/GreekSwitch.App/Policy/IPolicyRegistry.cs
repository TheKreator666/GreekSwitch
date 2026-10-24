namespace GreekSwitch.App.Policy;

/// <summary>Where a policy value comes from. Machine policy wins over user policy.</summary>
internal enum PolicyScope
{
    /// <summary>HKLM\Software\Policies\GreekSwitch.</summary>
    Machine,

    /// <summary>HKCU\Software\Policies\GreekSwitch.</summary>
    User,
}

/// <summary>Reads values from the policy keys, so tests can supply a fake registry.</summary>
internal interface IPolicyRegistry
{
    /// <summary>
    /// The raw value (int for REG_DWORD, string[] for REG_MULTI_SZ, string for REG_SZ), or null
    /// when the key or the value doesn't exist or can't be read.
    /// </summary>
    object? GetValue(PolicyScope scope, string name);
}
