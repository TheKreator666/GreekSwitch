using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Tests.Input;

public class KeyClassifierTests
{
    private readonly KeyClassifier _classifier = new(StaticLayouts.Greek, StaticLayouts.English);

    [Theory]
    [InlineData('A')]
    [InlineData('Q')]                 // ';' in Greek but a letter in English
    [InlineData(VirtualKeys.Oem1)]    // tonos in Greek, ';' in English
    [InlineData(VirtualKeys.Oem7)]    // apostrophe
    [InlineData('5')]
    public void Word_keys_go_into_the_buffer(int vk)
    {
        _classifier.Classify(new KeyInput(vk)).Action.Should().Be(KeyAction.Character);
    }

    [Theory]
    [InlineData(VirtualKeys.Space, false, DelimiterKind.Space, " ")]
    [InlineData(VirtualKeys.Return, false, DelimiterKind.Enter, "\n")]
    [InlineData(VirtualKeys.Tab, false, DelimiterKind.Tab, "\t")]
    [InlineData(VirtualKeys.OemPeriod, false, DelimiterKind.Punctuation, ".")]
    [InlineData(VirtualKeys.OemComma, false, DelimiterKind.Punctuation, ",")]
    [InlineData(VirtualKeys.Oem2, true, DelimiterKind.Punctuation, "?")]
    [InlineData('1', true, DelimiterKind.Punctuation, "!")]
    public void Boundary_keys_are_delimiters_with_their_text(int vk, bool shift, DelimiterKind kind, string text)
    {
        var result = _classifier.Classify(new KeyInput(vk, Shift: shift));

        result.Action.Should().Be(KeyAction.Delimiter);
        result.Delimiter.Should().Be(new Delimiter(kind, text));
    }

    [Theory]
    [InlineData(VirtualKeys.Left)]
    [InlineData(VirtualKeys.Home)]
    [InlineData(VirtualKeys.End)]
    [InlineData(VirtualKeys.Delete)]
    [InlineData(VirtualKeys.Escape)]
    [InlineData(0x70)] // F1
    [InlineData(0x60)] // numpad 0
    public void Caret_and_other_keys_reset(int vk)
    {
        _classifier.Classify(new KeyInput(vk)).Action.Should().Be(KeyAction.Reset);
    }

    [Fact]
    public void Shortcuts_reset()
    {
        _classifier.Classify(new KeyInput('C', Control: true)).Action.Should().Be(KeyAction.Reset);
        _classifier.Classify(new KeyInput('E', Control: true, Alt: true)).Action.Should().Be(KeyAction.Reset);
        _classifier.Classify(new KeyInput('D', Win: true)).Action.Should().Be(KeyAction.Reset);
    }

    [Theory]
    [InlineData(VirtualKeys.LShift)]
    [InlineData(VirtualKeys.Capital)]
    [InlineData(VirtualKeys.RControl)]
    public void Modifiers_alone_are_ignored(int vk)
    {
        _classifier.Classify(new KeyInput(vk)).Action.Should().Be(KeyAction.Ignore);
    }

    [Fact]
    public void Backspace_is_backspace()
    {
        _classifier.Classify(new KeyInput(VirtualKeys.Back)).Action.Should().Be(KeyAction.Backspace);
    }
}
