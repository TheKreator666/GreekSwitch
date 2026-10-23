using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Layouts;

/// <summary>
/// Startup check of the static layout maps against the user's installed layouts via ToUnicodeEx.
/// The OS is the source of truth: the returned map follows it wherever they differ, and every
/// difference is logged (by virtual-key code).
/// </summary>
public static class LayoutVerifier
{
    public static LayoutMap VerifyInstalled(LayoutMap staticMap, ILogger logger)
    {
        var hkl = KeyboardLayouts.FindInstalled(staticMap.Language);
        if (hkl == 0)
        {
            logger.LogWarning("No {Lang} layout installed; using the static map", staticMap.Language.Code());
            return staticMap;
        }

        var result = LayoutComparer.Verify(staticMap, new Win32KeyProbe(hkl));
        foreach (var mismatch in result.Mismatches)
        {
            logger.LogWarning(
                "{Lang} layout (HKL 0x{Hkl:X}): VK 0x{Vk:X2} {Detail}; using the OS behaviour",
                staticMap.Language.Code(),
                hkl,
                mismatch.VirtualKey,
                mismatch.Detail);
        }

        logger.LogInformation(
            "{Lang} layout verified against HKL 0x{Hkl:X}: {Count} mismatch(es)",
            staticMap.Language.Code(),
            hkl,
            result.Mismatches.Count);
        return result.Map;
    }

    /// <summary>
    /// ToUnicodeEx with a synthetic key state. Flushes any pending dead key after each probe so
    /// probes don't affect each other (this only touches the calling thread's keyboard state).
    /// </summary>
    private sealed unsafe class Win32KeyProbe(nint hkl) : IKeyProbe
    {
        public KeyOutput? Probe(int virtualKey, bool shift, bool capsLock)
        {
            var buffer = stackalloc char[8];
            var result = ToUnicode(virtualKey, shift, capsLock, buffer);
            if (result < 0)
            {
                var dead = buffer[0];
                Flush();
                return new KeyOutput(dead, IsDeadKey: true);
            }

            return result == 1 ? new KeyOutput(buffer[0], IsDeadKey: false) : null;
        }

        public char? Compose(KeyStroke deadKey, KeyStroke baseKey)
        {
            var buffer = stackalloc char[8];
            if (ToUnicode(deadKey.VirtualKey, deadKey.Shift, false, buffer) >= 0)
            {
                return null;
            }

            var result = ToUnicode(baseKey.VirtualKey, baseKey.Shift, false, buffer);
            if (result < 0)
            {
                Flush();
                return null;
            }

            return result == 1 ? buffer[0] : null;
        }

        private int ToUnicode(int vk, bool shift, bool capsLock, char* buffer)
        {
            var state = stackalloc byte[256];
            if (shift)
            {
                state[VirtualKeys.Shift] = 0x80;
            }

            if (capsLock)
            {
                state[VirtualKeys.Capital] = 0x01;
            }

            var scan = MapVirtualKeyExW((uint)vk, 0 /* MAPVK_VK_TO_VSC */, hkl);
            return ToUnicodeEx((uint)vk, scan, state, buffer, 8, 0, hkl);
        }

        private void Flush()
        {
            var buffer = stackalloc char[8];
            for (var i = 0; i < 3 && ToUnicode(VirtualKeys.Space, false, false, buffer) < 0; i++)
            {
            }
        }
    }
}
