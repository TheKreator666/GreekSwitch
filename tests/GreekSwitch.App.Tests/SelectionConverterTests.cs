using GreekSwitch.App.UI;
using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.TestSupport;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.App.Tests;

/// <summary>"Convert selection" against a fake clipboard and target app.</summary>
public sealed class SelectionConverterTests
{
    private const nint Window = 42;

    private readonly FakeApp _app = new();
    private readonly FakeLayouts _layouts = new();
    private readonly CapturingLogger _logger = new();
    private bool _modifierHeld;
    private nint _foreground = Window;
    private bool _commandWindow;

    [Fact]
    public async Task Pastes_the_converted_selection_switches_the_layout_and_restores_the_clipboard()
    {
        _app.Selection = "kalhm;era sas";
        _app.Clipboard.Contents = "what the user had copied";

        (await Converter().ConvertAsync()).Should().BeTrue();

        _app.Pasted.Should().Be("καλημέρα σας");
        _app.PastedWasPrivate.Should().BeTrue("the converted text must stay out of clipboard history");
        _layouts.SwitchedTo.Should().Be(Lang.Greek);
        _app.Clipboard.Contents.Should().Be("what the user had copied");
        _app.Clipboard.RestoredPrivately.Should().BeTrue();
    }

    [Fact]
    public async Task Without_a_selection_nothing_is_pasted_and_the_clipboard_is_untouched()
    {
        _app.Selection = null;
        _app.Clipboard.Contents = "kept";

        (await Converter().ConvertAsync()).Should().BeFalse();

        _app.Pasted.Should().BeNull();
        _app.Clipboard.Restores.Should().Be(0);
        _app.Clipboard.Contents.Should().Be("kept");
    }

    [Fact]
    public async Task Waits_for_the_hotkey_modifiers_and_gives_up_if_they_stay_down()
    {
        _modifierHeld = true;
        _app.Selection = "kalhm;era";

        (await Converter().ConvertAsync()).Should().BeFalse();

        _app.KeysSent.Should().Be(0, "Ctrl+C with Alt or Shift still down is another shortcut");
    }

    [Fact]
    public async Task Never_sends_ctrl_c_to_a_terminal()
    {
        _commandWindow = true;
        _app.Selection = "kalhm;era";

        (await Converter().ConvertAsync()).Should().BeFalse();

        _app.KeysSent.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_paste_into_another_window_and_still_restores_the_clipboard()
    {
        _app.Selection = "kalhm;era";
        _app.Clipboard.Contents = "original";
        _app.OnCopy = () => _foreground = 7;

        (await Converter().ConvertAsync()).Should().BeFalse();

        _app.Pasted.Should().BeNull();
        _app.Clipboard.Contents.Should().Be("original");
    }

    [Fact]
    public async Task Text_without_letters_is_left_alone_and_the_clipboard_restored()
    {
        _app.Selection = "2024 / 42";
        _app.Clipboard.Contents = "original";

        (await Converter().ConvertAsync()).Should().BeFalse();

        _app.Pasted.Should().BeNull();
        _app.Clipboard.Contents.Should().Be("original");
    }

    [Fact]
    public async Task Logs_lengths_only_never_the_text()
    {
        _app.Selection = "kalhm;era sas";
        _app.Clipboard.Contents = "secret clipboard";

        await Converter().ConvertAsync();

        _logger.AllOutput.Should().NotContainAny("kalhm", "καλημέρα", "secret");
    }

    private SelectionConverter Converter() => new(
        _app.Clipboard,
        _app,
        _layouts,
        new TextConverter(StaticLayouts.Greek, StaticLayouts.English),
        () => _modifierHeld,
        () => _foreground,
        _ => _commandWindow,
        _logger,
        _ => Task.CompletedTask);

    private sealed class FakeClipboard : IClipboardAccess
    {
        public string? Contents { get; set; }

        public bool ContentsPrivate { get; private set; }

        public uint SequenceNumber { get; private set; } = 1;

        public int Restores { get; private set; }

        public bool RestoredPrivately { get; private set; }

        public object? Backup() => Contents is null ? null : new Box(Contents);

        public void Restore(object? backup)
        {
            Restores++;
            Write((backup as Box)?.Text, isPrivate: true);
            RestoredPrivately = true;
        }

        public string? GetText() => Contents;

        public void SetPrivateText(string text) => Write(text, isPrivate: true);

        /// <summary>What the target app does on Ctrl+C.</summary>
        public void Write(string? text, bool isPrivate = false)
        {
            Contents = text;
            ContentsPrivate = isPrivate;
            SequenceNumber++;
        }

        private sealed record Box(string Text);
    }

    /// <summary>The focused app: Ctrl+C copies its selection (if any), Ctrl+V pastes the clipboard.</summary>
    private sealed class FakeApp : IInputSender
    {
        public FakeClipboard Clipboard { get; } = new();

        public string? Selection { get; set; }

        public string? Pasted { get; private set; }

        public bool PastedWasPrivate { get; private set; }

        public int KeysSent { get; private set; }

        public Action? OnCopy { get; set; }

        public uint Send(INPUT[] inputs)
        {
            KeysSent += inputs.Length;
            var key = inputs.Select(i => i.U.ki.wVk).First(vk => vk != VirtualKeys.Control);
            if (key == 'C' && Selection is not null)
            {
                Clipboard.Write(Selection);
                OnCopy?.Invoke();
            }
            else if (key == 'V')
            {
                Pasted = Clipboard.Contents;
                PastedWasPrivate = Clipboard.ContentsPrivate;
            }

            return (uint)inputs.Length;
        }
    }

    private sealed class FakeLayouts : ILayoutSwitcher
    {
        public Lang? SwitchedTo { get; private set; }

        public Lang? CurrentLayout() => SwitchedTo ?? Lang.English;

        public void SwitchTo(Lang lang) => SwitchedTo = lang;

        public void SwitchWithHotkey()
        {
        }
    }
}
