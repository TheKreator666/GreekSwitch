using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Tests.Layout;

public class KeyPlannerTests
{
    private readonly DualDecoder _decoder = new(StaticLayouts.Greek, StaticLayouts.English);

    [Theory]
    [InlineData("application ", Lang.English)]
    [InlineData("Hello, World! ", Lang.English)]
    [InlineData("don't ", Lang.English)]
    [InlineData("ωρα ", Lang.Greek)]
    [InlineData("καλημέρα ", Lang.Greek)]
    [InlineData("πράξεις.", Lang.Greek)]
    [InlineData("Άλφα ΩΜΕΓΑ ", Lang.Greek)]
    [InlineData("προϊόν ", Lang.Greek)]
    [InlineData("ΐ ΰ ", Lang.Greek)]
    [InlineData("τι;", Lang.Greek)]
    public void Planned_keys_decode_back_to_the_text(string text, Lang lang)
    {
        foreach (var capsLock in new[] { false, true })
        {
            var keys = KeyPlanner.Plan(text, StaticLayouts.For(lang), capsLock);

            keys.Should().NotBeNull();
            var withoutSpaces = keys!.Where(k => k.VirtualKey != VirtualKeys.Space).ToList();
            _decoder.Decode(withoutSpaces, lang).Should().Be(text.Replace(" ", ""), $"caps lock {(capsLock ? "on" : "off")}");
        }
    }

    [Fact]
    public void Tonos_is_typed_with_the_dead_key_then_the_vowel()
    {
        KeyPlanner.Plan("ά", StaticLayouts.Greek, capsLock: false).Should().Equal(
            new KeyStroke(VirtualKeys.Oem1), new KeyStroke('A'));
    }

    [Fact]
    public void Final_sigma_uses_W_and_sigma_uses_S()
    {
        KeyPlanner.Plan("σς", StaticLayouts.Greek, capsLock: false).Should().Equal(
            new KeyStroke('S'), new KeyStroke('W'));
    }

    [Fact]
    public void Caps_lock_flips_shift_for_letters_only()
    {
        KeyPlanner.Plan("aA1", StaticLayouts.English, capsLock: true).Should().Equal(
            new KeyStroke('A', Shift: true, CapsLock: true),
            new KeyStroke('A', Shift: false, CapsLock: true),
            new KeyStroke('1', Shift: false, CapsLock: true));
    }

    [Fact]
    public void Enter_and_tab_map_to_their_keys()
    {
        KeyPlanner.Plan("\n\t", StaticLayouts.English, capsLock: false).Should().Equal(
            new KeyStroke(VirtualKeys.Return), new KeyStroke(VirtualKeys.Tab));
    }

    [Fact]
    public void Text_with_a_character_the_layout_cannot_type_has_no_plan()
    {
        KeyPlanner.Plan("ωρα", StaticLayouts.English, capsLock: false).Should().BeNull();
        KeyPlanner.Plan("€", StaticLayouts.Greek, capsLock: false).Should().BeNull();
    }
}
