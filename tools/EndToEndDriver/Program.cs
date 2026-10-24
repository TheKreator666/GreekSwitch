// End-to-end check in a real Notepad: wrong-layout sentences, both convert hotkeys and undo.
//
//   1. Close every Notepad window (the driver refuses to start otherwise, and closes Notepad at the end).
//   2. dotnet build src/GreekSwitch.App -c Debug
//      $env:GREEKSWITCH_TEST_ACCEPT_INJECTED = '1'; src/GreekSwitch.App/bin/Debug/net8.0-windows/GreekSwitch.exe
//   3. dotnet run --project tools/EndToEndDriver      (don't touch the keyboard for about a minute)
//
// Exit code 0 when every check passes. The hotkey conversion is undone, so it learns nothing; the
// loanword check records one accept and one reject (hashed) in settings.json: back it up first.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GreekSwitch.Core;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using Microsoft.Extensions.Logging.Abstractions;

internal static class Program
{
    private const ushort Ctrl = 0x11, Alt = 0x12, Shift = 0x10, Space = 0x20, Back = 0x08, Tab = 0x09, Delete = 0x2E, Escape = 0x1B;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreekSwitch", "greekswitch.log");

    private static readonly LayoutSwitcher Layouts = new(NullLogger.Instance);
    private static int _logMark;
    private static int _failures;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (Process.GetProcessesByName("Notepad").Length > 0)
        {
            Console.Error.WriteLine("Close Notepad first: the driver types into Notepad and closes it at the end.");
            return 2;
        }

        if (Process.GetProcessesByName("GreekSwitch").Length == 0 || !File.Exists(LogPath))
        {
            Console.Error.WriteLine("Start a Debug GreekSwitch with GREEKSWITCH_TEST_ACCEPT_INJECTED=1 first.");
            return 2;
        }

        using var notepad = Process.Start("notepad.exe");
        Thread.Sleep(3000);
        Tap(Escape);
        Chord('N', Ctrl); // a fresh tab: never touch restored session tabs
        Thread.Sleep(1000);
        _logMark = File.ReadAllLines(LogPath).Length;

        try
        {
            for (var trial = 1; trial <= 3; trial++)
            {
                TypeInEnglish("vra na testaroyme to n;eo application ");
                Check($"sentence {trial}", ReadAndClear(), "ωρα να τεσταρουμε το νέο application ");
            }

            // "to" is an English word, so it is kept; the hotkey converts it, Backspace undoes that.
            TypeInEnglish("to ");
            Chord(Space, Ctrl, Alt);
            Thread.Sleep(1200);
            CheckLog("convert last word: converted", "Last word converted by hotkey, lang EN→GR, length 2");
            Tap(Back);
            Thread.Sleep(800);
            Check("convert last word: undone", ReadAndClear(), "to");

            // Convert selection: the text is converted and the user's clipboard survives.
            SetClipboard("keep me");
            TypeInEnglish("the end ");
            Chord('A', Ctrl);
            Thread.Sleep(200);
            Chord(Space, Ctrl, Alt, Shift);
            Thread.Sleep(2000);
            Check("convert selection: clipboard restored", GetClipboard(), "keep me");
            Check("convert selection: text", ReadAndClear(), "τηε ενδ ");

            // Loanword typed correctly in Greek: suggested, Tab accepts, Backspace undoes.
            TypeIn(Lang.Greek, "kont;einer ");
            CheckLog("loanword: suggested", "Suggestion shown, kind loanword, length 9");
            Tap(Tab);
            Thread.Sleep(800);
            CheckLog("loanword: accepted", "Suggestion accepted, kind loanword, length 9");
            Tap(Back);
            Thread.Sleep(800);
            Check("loanword: accepted then undone", ReadAndClear(), "κοντέινερ");

            // Tab with no suggestion on screen is an ordinary Tab.
            TypeIn(Lang.Greek, "spi;ti ");
            Tap(Tab);
            Thread.Sleep(500);
            Check("plain tab passes through", ReadAndClear(), "σπιτι 	");
        }
        catch (FocusLostException)
        {
            Console.Error.WriteLine("Stopped: Notepad is no longer the foreground window. No further keys were sent.");
            _failures++;
        }
        finally
        {
            // Never leave a modifier down, whatever happened (key-ups are harmless anywhere).
            foreach (var m in new[] { Ctrl, Alt, Shift })
            {
                _ = Native.SendInput(1, [Key(m, up: true)], Marshal.SizeOf<INPUT>());
            }

            foreach (var p in Process.GetProcessesByName("Notepad"))
            {
                p.Kill();
            }
        }

        Console.WriteLine(_failures == 0 ? "All checks passed." : $"{_failures} check(s) failed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void TypeInEnglish(string keys) => TypeIn(Lang.English, keys);

    private static void TypeIn(Lang layout, string keys)
    {
        Layouts.SwitchTo(layout);
        Thread.Sleep(500);

        // Physical keys: letters by name, ';' = the tonos key (VK_OEM_1), ' ' = Space.
        foreach (var c in keys)
        {
            ushort vk = c switch { ' ' => Space, ';' => 0xBA, _ => char.ToUpperInvariant(c) };
            Tap(vk, c == ' ' ? 80 : 30);
            Thread.Sleep(150);
        }

        Thread.Sleep(1000);
    }

    private static void Check(string name, string actual, string expected)
    {
        var ok = actual == expected;
        _failures += ok ? 0 : 1;
        Console.WriteLine($"--- {name}: [{actual}] {(ok ? "OK" : $"FAILED, expected [{expected}]")}");
        PrintNewLogLines();
    }

    private static void CheckLog(string name, string expected)
    {
        var lines = File.ReadAllLines(LogPath).Skip(_logMark).ToList();
        var ok = lines.Any(l => l.Contains(expected, StringComparison.Ordinal));
        _failures += ok ? 0 : 1;
        Console.WriteLine($"--- {name}: {(ok ? "OK" : $"FAILED, no log line [{expected}]")}");
        PrintNewLogLines();
    }

    private static void PrintNewLogLines()
    {
        var lines = File.ReadAllLines(LogPath);
        foreach (var line in lines.Skip(_logMark))
        {
            Console.WriteLine("    log: " + line[11..]);
        }

        _logMark = lines.Length;
    }

    private static string ReadAndClear()
    {
        Chord('A', Ctrl);
        Chord('C', Ctrl);
        Thread.Sleep(300);
        var text = GetClipboard();
        Tap(Delete);
        Thread.Sleep(300);
        return text;
    }

    private static string GetClipboard() => Sta(() => Clipboard.ContainsText() ? Clipboard.GetText() : "<no text>");

    private static void SetClipboard(string text) => Sta(() =>
    {
        Clipboard.SetText(text);
        return "";
    });

    private static string Sta(Func<string> action)
    {
        var result = "";
        var thread = new Thread(() => result = action());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private static void Tap(ushort vk, int holdMs = 10)
    {
        Send(Key(vk, up: false));
        Thread.Sleep(holdMs);
        Send(Key(vk, up: true));
    }

    /// <summary>Modifiers down, a key tap, a short hold, modifiers up: like a person.</summary>
    private static void Chord(ushort key, params ushort[] modifiers)
    {
        foreach (var m in modifiers)
        {
            Send(Key(m, up: false));
            Thread.Sleep(20);
        }

        Tap(key, 40);
        Thread.Sleep(80);
        foreach (var m in Enumerable.Reverse(modifiers))
        {
            Send(Key(m, up: true));
            Thread.Sleep(20);
        }
    }

    /// <summary>
    /// Sends one key event, but only into Notepad. A key-down sent anywhere else is dangerous: in
    /// Explorer, Ctrl+A then Delete deletes every file in the folder, and Backspace goes up a level.
    /// </summary>
    private static void Send(INPUT input)
    {
        var isKeyDown = (input.U.ki.dwFlags & 2u) == 0;
        if (isKeyDown && !NotepadIsForeground())
        {
            throw new FocusLostException();
        }

        if (Native.SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1)
        {
            throw new InvalidOperationException("SendInput was blocked (is a window running as administrator in front?)");
        }
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = 1, // INPUT_KEYBOARD
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)Native.MapVirtualKeyW(vk, 0), dwFlags = up ? 2u : 0u } },
    };

    private static bool NotepadIsForeground()
    {
        _ = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var pid);
        return Process.GetProcessesByName("Notepad").Any(p => p.Id == pid);
    }

    private sealed class FocusLostException : Exception;

    private static class Native
    {
        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);

        [DllImport("user32.dll")]
        public static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKeyW(uint code, uint type);
    }
}
