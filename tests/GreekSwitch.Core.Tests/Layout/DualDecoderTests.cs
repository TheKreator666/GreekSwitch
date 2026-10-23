using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Layout;

public class DualDecoderTests
{
    private readonly DualDecoder _decoder = new(StaticLayouts.Greek, StaticLayouts.English);

    private static KeyStroke K(char letter, bool shift = false, bool caps = false) => new(letter, shift, caps);

    private static KeyStroke Tonos => new(VirtualKeys.Oem1);

    private static KeyStroke Dialytika => new(VirtualKeys.Oem1, Shift: true);

    private static KeyStroke DialytikaTonos => new('W', Shift: true);

    public static TheoryData<char, string, string> Letters()
    {
        var data = new TheoryData<char, string, string>();
        const string greek = "αβψδεφγηιξκλμνοπ;ρστθωςχυζ";
        for (var i = 0; i < 26; i++)
        {
            var key = (char)('A' + i);
            data.Add(key, greek[i].ToString(), char.ToLowerInvariant(key).ToString());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Letters))]
    public void Every_letter_key_decodes_in_both_layouts(char key, string greek, string english)
    {
        _decoder.Decode([K(key)]).Should().Be((greek, english));
    }

    [Theory]
    [InlineData('A', "Α", "A")]
    [InlineData('C', "Ψ", "C")]
    [InlineData('S', "Σ", "S")]
    [InlineData('U', "Θ", "U")]
    [InlineData('V', "Ω", "V")]
    [InlineData('Q', ":", "Q")]
    public void Shift_gives_uppercase_or_shifted_symbol(char key, string greek, string english)
    {
        _decoder.Decode([K(key, shift: true)]).Should().Be((greek, english));
    }

    [Fact]
    public void Q_is_the_greek_question_mark()
    {
        _decoder.Decode([K('Q')], Lang.Greek).Should().Be(";");
    }

    [Theory]
    [InlineData('A', "ά", "Ά")]
    [InlineData('E', "έ", "Έ")]
    [InlineData('H', "ή", "Ή")]
    [InlineData('I', "ί", "Ί")]
    [InlineData('O', "ό", "Ό")]
    [InlineData('Y', "ύ", "Ύ")]
    [InlineData('V', "ώ", "Ώ")]
    public void Tonos_dead_key_accents_each_vowel(char vowel, string lower, string upper)
    {
        _decoder.Decode([Tonos, K(vowel)], Lang.Greek).Should().Be(lower);
        _decoder.Decode([Tonos, K(vowel, shift: true)], Lang.Greek).Should().Be(upper);
    }

    [Fact]
    public void Tonos_key_reads_as_semicolon_in_english()
    {
        _decoder.Decode([Tonos, K('A')], Lang.English).Should().Be(";a");
    }

    [Theory]
    [InlineData('I', "ϊ", "Ϊ")]
    [InlineData('Y', "ϋ", "Ϋ")]
    public void Shift_tonos_key_is_dialytika(char vowel, string lower, string upper)
    {
        _decoder.Decode([Dialytika, K(vowel)], Lang.Greek).Should().Be(lower);
        _decoder.Decode([Dialytika, K(vowel, shift: true)], Lang.Greek).Should().Be(upper);
    }

    [Theory]
    [InlineData('I', "ΐ")]
    [InlineData('Y', "ΰ")]
    public void Dialytika_tonos_dead_key_gives_combined_accent(char vowel, string expected)
    {
        _decoder.Decode([DialytikaTonos, K(vowel)], Lang.Greek).Should().Be(expected);
    }

    [Fact]
    public void Dead_key_followed_by_a_letter_it_cannot_accent_emits_both()
    {
        _decoder.Decode([Tonos, K('K')], Lang.Greek).Should().Be("΄κ");
        _decoder.Decode([Dialytika, K('A')], Lang.Greek).Should().Be("¨α");
    }

    [Fact]
    public void Two_dead_keys_in_a_row_emit_both_spacing_accents()
    {
        _decoder.Decode([Tonos, Tonos], Lang.Greek).Should().Be("΄΄");
    }

    [Fact]
    public void Trailing_dead_key_emits_its_spacing_accent()
    {
        _decoder.Decode([K('K'), Tonos], Lang.Greek).Should().Be("κ΄");
    }

    [Fact]
    public void W_is_final_sigma_and_S_is_sigma()
    {
        _decoder.Decode([K('W')], Lang.Greek).Should().Be("ς");
        _decoder.Decode([K('S')], Lang.Greek).Should().Be("σ");
    }

    [Fact]
    public void Caps_lock_uppercases_letters_in_both_layouts()
    {
        var keys = "KALI".Select(c => K(c, caps: true)).ToList();
        _decoder.Decode(keys).Should().Be(("ΚΑΛΙ", "KALI"));
    }

    [Fact]
    public void Caps_lock_with_shift_gives_lowercase()
    {
        _decoder.Decode([K('A', shift: true, caps: true)]).Should().Be(("α", "a"));
    }

    [Fact]
    public void Caps_lock_does_not_affect_digits_or_final_sigma()
    {
        _decoder.Decode([K('1', caps: true), K('W', caps: true)], Lang.Greek).Should().Be("1ς");
    }

    [Fact]
    public void Caps_lock_with_tonos_gives_accented_capital()
    {
        _decoder.Decode([Tonos, K('A', caps: true)], Lang.Greek).Should().Be("Ά");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("Hello")]
    [InlineData("HELLO")]
    public void Case_pattern_is_preserved_across_readings(string english)
    {
        var keys = Typist.Type(english, Lang.English);
        var (greek, decodedEnglish) = _decoder.Decode(keys);

        decodedEnglish.Should().Be(english);
        greek.Should().Be(english switch
        {
            "hello" => "ηελλο",
            "Hello" => "Ηελλο",
            _ => "ΗΕΛΛΟ",
        });
    }

    [Theory]
    [InlineData("αππλιψατιον", "application")]
    [InlineData("που", "poy")]
    [InlineData("θα", "ua")]
    [InlineData("καλημέρα", "kalhm;era")]
    [InlineData("ςορλδ", "world")]
    public void Same_keys_have_a_greek_and_an_english_reading(string greek, string english)
    {
        _decoder.Decode(Typist.Type(greek, Lang.Greek)).Should().Be((greek, english));
        _decoder.Decode(Typist.Type(english, Lang.English)).Should().Be((greek, english));
    }

    [Fact]
    public void Punctuation_and_digits_are_the_same_in_both_layouts()
    {
        var keys = Typist.Type("1,.!?()-'\"", Lang.English);
        _decoder.Decode(keys).Should().Be(("1,.!?()-'\"", "1,.!?()-'\""));
    }
}
