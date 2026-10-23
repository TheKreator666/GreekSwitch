using GreekSwitch.Core;
using GreekSwitch.Windows.Layouts;

namespace GreekSwitch.Windows.Tests.Layouts;

public class KeyboardLayoutsTests
{
    [Theory]
    [InlineData(0x04080408L, Lang.Greek)]      // Greek
    [InlineData(unchecked((long)0xF0A80408L), Lang.Greek)] // a Greek layout variant
    [InlineData(0x04090409L, Lang.English)]    // US
    [InlineData(0x08090809L, Lang.English)]    // UK
    [InlineData(unchecked((long)0xF0010409L), Lang.English)] // US-International
    public void Maps_hkl_to_language_by_primary_language_id(long hkl, Lang expected)
    {
        KeyboardLayouts.ToLang((nint)hkl).Should().Be(expected);
    }

    [Theory]
    [InlineData(0x04070407L)] // German
    [InlineData(0x04190419L)] // Russian
    [InlineData(0L)]
    public void Other_languages_map_to_null(long hkl)
    {
        KeyboardLayouts.ToLang((nint)hkl).Should().BeNull();
    }
}
