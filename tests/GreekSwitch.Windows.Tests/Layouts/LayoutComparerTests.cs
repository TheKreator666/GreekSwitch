using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Layouts;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.Windows.Tests.Layouts;

public class LayoutComparerTests
{
    [Theory]
    [InlineData(Lang.Greek)]
    [InlineData(Lang.English)]
    public void A_layout_that_matches_the_static_map_has_no_mismatches(Lang lang)
    {
        var map = StaticLayouts.For(lang);

        var result = LayoutComparer.Verify(map, new MapProbe(map));

        result.Mismatches.Should().BeEmpty();
        result.Map.Keys.Should().BeEquivalentTo(map.Keys);
        result.Map.Compositions.Should().BeEquivalentTo(map.Compositions);
    }

    [Fact]
    public void The_os_wins_where_a_key_differs()
    {
        // Pretend the OS layout has Shift+W = Σ (a plain letter) instead of the ΅ dead key.
        var os = StaticLayouts.Greek.WithOverrides(new Dictionary<int, KeyDefinition> { ['W'] = new('ς', 'Σ') });

        var result = LayoutComparer.Verify(StaticLayouts.Greek, new MapProbe(os));

        result.Mismatches.Should().Contain(m => m.VirtualKey == 'W');
        result.Map.TryMap(new KeyStroke('W', Shift: true), out var output).Should().BeTrue();
        output.Should().Be(new KeyOutput('Σ', false));
    }

    [Fact]
    public void The_os_wins_where_a_composition_differs()
    {
        var os = StaticLayouts.Greek.WithOverrides(compositions: new Dictionary<(char, char), char> { [(StaticLayouts.Tonos, 'α')] = 'X' });

        var result = LayoutComparer.Verify(StaticLayouts.Greek, new MapProbe(os));

        result.Map.Compose(StaticLayouts.Tonos, 'α').Should().Be('X');
        result.Mismatches.Should().ContainSingle(m => m.VirtualKey == VirtualKeys.Oem1);
    }

    [Fact]
    public void Mismatch_details_describe_keys_not_characters()
    {
        var os = StaticLayouts.English.WithOverrides(new Dictionary<int, KeyDefinition> { ['Q'] = new('x', 'y') });

        var mismatch = LayoutComparer.Verify(StaticLayouts.English, new MapProbe(os)).Mismatches.Single();

        mismatch.Detail.Should().NotContain("x").And.NotContain("y");
    }

    /// <summary>Only meaningful on a Windows machine with both layouts installed; passes trivially elsewhere.</summary>
    [Theory]
    [InlineData(Lang.Greek)]
    [InlineData(Lang.English)]
    public void Static_map_matches_the_installed_os_layout_when_present(Lang lang)
    {
        if (!OperatingSystem.IsWindows() || KeyboardLayouts.FindInstalled(lang) == 0)
        {
            return;
        }

        var verified = LayoutVerifier.VerifyInstalled(StaticLayouts.For(lang), NullLogger.Instance);

        verified.Keys.Should().BeEquivalentTo(StaticLayouts.For(lang).Keys);
        verified.Compositions.Should().BeEquivalentTo(StaticLayouts.For(lang).Compositions);
    }

    /// <summary>An IKeyProbe that answers from a LayoutMap, emulating ToUnicodeEx.</summary>
    private sealed class MapProbe(LayoutMap map) : IKeyProbe
    {
        public KeyOutput? Probe(int virtualKey, bool shift, bool capsLock) =>
            map.TryMap(new KeyStroke(virtualKey, shift, capsLock), out var output) ? output : null;

        public char? Compose(KeyStroke deadKey, KeyStroke baseKey)
        {
            if (!map.TryMap(deadKey, out var dead) || !dead.IsDeadKey || !map.TryMap(baseKey, out var b))
            {
                return null;
            }

            return map.Compose(dead.Character, b.Character);
        }
    }
}
