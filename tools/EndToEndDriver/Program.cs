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

internal static partial class Program
{
    private const ushort Ctrl = 0x11, Alt = 0x12, Shift = 0x10, Space = 0x20, Back = 0x08, Tab = 0x09, Delete = 0x2E, Escape = 0x1B;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreekSwitch", "greekswitch.log");

    private static readonly LayoutSwitcher Layouts = new(NullLogger.Instance);
    private static int _logMark;
    private static int _failures;

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (Process.GetProcessesByName("Notepad").Length > 0)
        {
            Console.Error.WriteLine("Close Notepad first: the driver types into Notepad and closes it at the end.");
            return 2;
        }

        // --probe: GreekSwitch must NOT run. Replays the corrector's batch by hand to see how
        // Notepad handles it when the user keeps typing.
        if (args is ["--probe"])
        {
            return Process.GetProcessesByName("GreekSwitch").Length > 0
                ? Fail("Stop GreekSwitch first: the probe sends the corrections itself.")
                : ProbeScenario();
        }

        if (Process.GetProcessesByName("GreekSwitch").Length == 0 || !File.Exists(LogPath))
        {
            Console.Error.WriteLine("Start a Debug GreekSwitch with GREEKSWITCH_TEST_ACCEPT_INJECTED=1 first.");
            return 2;
        }

        // --matrix <work dir>: the same wrong-layout and accent cases in Notepad, Word and browsers.
        if (args is ["--matrix", var workDir])
        {
            return Matrix.Run(workDir);
        }

        // --append <file>: type Greek without accents at the end of an existing text file, at a
        // slow and a fast typing speed, print what came out, save the file. Nothing is deleted.
        if (args is ["--append", var file])
        {
            return AppendScenario(file);
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
            TypeIn(Lang.Greek, "sp;iti "); // the tonos key comes before its vowel
            Tap(Tab);
            Thread.Sleep(500);
            Check("plain tab passes through", ReadAndClear(), "σπίτι 	");
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

    /// <summary>Physical keys typed in the Greek layout (no tonos key), and what should come out.</summary>
    private static readonly (string Keys, string Expected)[] AppendLines =
    [
        ("doyleyei tvra to programma kai exoyme pali uemata ", "δουλεύει τώρα το πρόγραμμα και έχουμε πάλι θέματα "),
        ("elegxoyme pali ama to systhma doyleyei kala ", "ελέγχουμε πάλι άμα το σύστημα δουλεύει καλά "),
        ("exv ena forthgo poy prepei na fortvuei sto limani ", "έχω ένα φορτηγό που πρέπει να φορτωθεί στο λιμάνι "),
    ];

    private static int AppendScenario(string file)
    {
        using var notepad = Process.Start("notepad.exe", $"\"{file}\"");
        Thread.Sleep(3000);
        _logMark = File.ReadAllLines(LogPath).Length;
        try
        {
            // Windows won't let a background process take the foreground: ask, then wait for a click.
            foreach (var p in Process.GetProcessesByName("Notepad"))
            {
                _ = Native.SetForegroundWindow(p.MainWindowHandle);
            }

            if (!NotepadIsForeground())
            {
                Console.WriteLine("Click into the Notepad window to start (waiting up to 20 s)...");
            }

            var clock = Stopwatch.StartNew();
            while (!NotepadIsForeground() && clock.Elapsed < TimeSpan.FromSeconds(20))
            {
                Thread.Sleep(200);
            }

            Thread.Sleep(1000); // the click itself settles
            foreach (var (name, gapMs) in new[] { ("slow, 150 ms between keys", 150), ("fast, 60 ms between keys", 60) })
            {
                foreach (var (keys, expected) in AppendLines)
                {
                    var before = ReadAll();
                    Chord(0x23, Ctrl); // Ctrl+End: the caret after the existing text, no selection
                    Tap(0x0D);         // Enter: a new line (not a correctable delimiter)
                    TypeIn(Lang.Greek, keys, gapMs);
                    var after = ReadAll();
                    Chord(0x23, Ctrl);
                    var typed = after.Length > before.Length ? after[(before.Length)..].TrimStart('\r', '\n') : "<text lost>";
                    Check($"{name}", typed, expected);
                }
            }

            Chord('S', Ctrl); // save the file with the lines added
            Thread.Sleep(1000);
        }
        catch (FocusLostException)
        {
            Console.Error.WriteLine("Stopped: Notepad is no longer the foreground window. No further keys were sent.");
            _failures++;
        }
        finally
        {
            foreach (var m in new[] { Ctrl, Alt, Shift })
            {
                _ = Native.SendInput(1, [Key(m, up: true)], Marshal.SizeOf<INPUT>());
            }

            foreach (var p in Process.GetProcessesByName("Notepad"))
            {
                p.CloseMainWindow();
            }
        }

        Console.WriteLine(_failures == 0 ? "All checks passed." : $"{_failures} check(s) failed.");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>An empty low-level keyboard hook on its own thread, like GreekSwitch's. Returns a stop action.</summary>
    private static Action StartEmptyHook(ThreadPriority priority)
    {
        uint threadId = 0;
        var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            threadId = Native.GetCurrentThreadId();
            HookProc proc = (code, w, l) => Native.CallNextHookEx(0, code, w, l);
            var hook = Native.SetWindowsHookExW(13 /* WH_KEYBOARD_LL */, proc, Native.GetModuleHandleW(null), 0);
            ready.Set();
            while (Native.GetMessageW(out _, 0, 0, 0) > 0)
            {
            }

            Native.UnhookWindowsHookEx(hook);
            GC.KeepAlive(proc);
        }) { IsBackground = true, Priority = priority };
        thread.Start();
        ready.Wait();
        Thread.Sleep(300);
        return () =>
        {
            Native.PostThreadMessageW(threadId, 0x0012 /* WM_QUIT */, 0, 0);
            thread.Join(2000);
        };
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    /// <summary>
    /// Types "δουλευει " (Greek layout, no tonos), sends the corrector's batch (9 Backspaces, then
    /// "δουλεύει " as real keys with the dead key, or as Unicode), waits, then types "καλα ".
    /// </summary>
    private static int ProbeScenario()
    {
        _logMark = File.ReadAllLines(LogPath).Length; // GreekSwitch is off: nothing new is logged
        using var notepad = Process.Start("notepad.exe");
        Thread.Sleep(3000);
        foreach (var p in Process.GetProcessesByName("Notepad"))
        {
            _ = Native.SetForegroundWindow(p.MainWindowHandle);
        }

        var clock = Stopwatch.StartNew();
        while (!NotepadIsForeground() && clock.Elapsed < TimeSpan.FromSeconds(20))
        {
            Thread.Sleep(200);
        }

        Thread.Sleep(1000);
        try
        {
            Tap(Escape);
            Chord('N', Ctrl); // a fresh tab
            Thread.Sleep(1000);

            // Does Notepad itself rewrite words after Space (its own autocorrect)?
            TypeIn(Lang.Greek, "programma ena fortvuei doyleyei elegxoyme pali ", 150);
            Thread.Sleep(2000);
            Check("Notepad on its own (GreekSwitch off)", ReadAndClear(), "προγραμμα ενα φορτωθει δουλευει ελεγχουμε παλι ");
            if (Environment.GetEnvironmentVariable("PROBE_AUTOCORRECT_ONLY") == "1")
            {
                return _failures == 0 ? 0 : 1;
            }

            // Like the app: every word is corrected right after its Space, while typing goes on.
            using var uia = new GreekSwitch.Windows.Focus.UiaThread();
            var verifier = new GreekSwitch.Windows.Focus.UiaTextVerifier(uia);
            const string Keys = "elegxoyme pali ama to systhma doyleyei kala ";
            const string Expected = "ελέγχουμε πάλι άμα το σύστημα δουλεύει καλά ";

            // The user's next key arrives while the correction batch is being delivered.
            var plan1 = GreekSwitch.Core.Layout.KeyPlanner.Plan("δουλεύει ", GreekSwitch.Core.Layout.StaticLayouts.Greek, capsLock: false)!;
            foreach (var (hook, priority) in new[] { ("empty hook", ThreadPriority.Normal), ("no hook", ThreadPriority.Normal), ("empty hook, high priority", ThreadPriority.Highest) })
            {
                var stopHook = hook == "no hook" ? null : StartEmptyHook(priority);
                for (var trial = 1; trial <= 3; trial++)
                {
                    TypeIn(Lang.Greek, "doyleyei ", 150);
                    var batch = GreekSwitch.Windows.Output.InputBuilder.BuildKeys(9, plan1, vk => (ushort)Native.MapVirtualKeyW((uint)vk, 0));
                    if (!NotepadIsForeground())
                    {
                        throw new FocusLostException();
                    }

                    var typist = new Thread(() =>
                    {
                        Thread.Sleep(10); // mid-flight
                        Tap('K', 30);
                    });
                    var send = Stopwatch.StartNew();
                    typist.Start();
                    _ = Native.SendInput((uint)batch.Length, batch, Marshal.SizeOf<INPUT>());
                    var sendMs = send.ElapsedMilliseconds;
                    typist.Join();
                    Thread.Sleep(150);
                    TypeIn(Lang.Greek, "ala ", 150);
                    Check($"{hook}: next key 10 ms into the send (send took {sendMs} ms), trial {trial}", ReadAndClear(), "δουλεύει καλα ");
                }

                stopHook?.Invoke();
            }

            Thread.Sleep(200);
            foreach (var (withUia, unicode) in Array.Empty<(bool, bool)>())
            {
                var bare = GreekSwitch.Core.Text.GreekText.StripDiacritics(Expected).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var accented = Expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Layouts.SwitchTo(Lang.Greek);
                Thread.Sleep(500);
                var word = 0;
                foreach (var c in Keys)
                {
                    Tap(c == ' ' ? Space : char.ToUpperInvariant(c), 30);
                    if (c == ' ')
                    {
                        if (bare[word] != accented[word])
                        {
                            Thread.Sleep(5); // the worker gets to the Space
                            if (withUia)
                            {
                                _ = verifier.BeforeCaret(bare[word] + " ");
                            }

                            var keys = GreekSwitch.Core.Layout.KeyPlanner.Plan(accented[word] + " ", GreekSwitch.Core.Layout.StaticLayouts.Greek, capsLock: false)!;
                            var b = unicode
                                ? GreekSwitch.Windows.Output.InputBuilder.Build(bare[word].Length + 1, accented[word] + " ")
                                : GreekSwitch.Windows.Output.InputBuilder.BuildKeys(bare[word].Length + 1, keys, vk => (ushort)Native.MapVirtualKeyW((uint)vk, 0));
                            if (!NotepadIsForeground())
                            {
                                throw new FocusLostException();
                            }

                            _ = Native.SendInput((uint)b.Length, b, Marshal.SizeOf<INPUT>());
                        }

                        word++;
                    }

                    Thread.Sleep(150);
                }

                Thread.Sleep(1000);
                Check($"sentence with an empty LL hook, {(unicode ? "unicode" : "dead key")}", ReadAndClear(), Expected);
            }

            var plan = GreekSwitch.Core.Layout.KeyPlanner.Plan("δουλεύει ", GreekSwitch.Core.Layout.StaticLayouts.Greek, capsLock: false)!;
            foreach (var (variant, delay) in Array.Empty<(string, int)>())
            {
                for (var trial = 1; trial <= 2; trial++)
                {
                    TypeIn(Lang.Greek, "doyleyei ", 150);
                    var batch = variant == "unicode"
                        ? GreekSwitch.Windows.Output.InputBuilder.Build(9, "δουλεύει ")
                        : GreekSwitch.Windows.Output.InputBuilder.BuildKeys(9, plan, vk => (ushort)Native.MapVirtualKeyW((uint)vk, 0));
                    if (!NotepadIsForeground())
                    {
                        throw new FocusLostException();
                    }

                    _ = Native.SendInput((uint)batch.Length, batch, Marshal.SizeOf<INPUT>());
                    Thread.Sleep(delay);
                    TypeIn(Lang.Greek, "kala ", 150);
                    Check($"{variant}, next key after {delay} ms, trial {trial}", ReadAndClear(), "δουλεύει καλα ");
                }
            }
        }
        catch (FocusLostException)
        {
            Console.Error.WriteLine("Stopped: Notepad is no longer the foreground window. No further keys were sent.");
            _failures++;
        }
        finally
        {
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

    /// <summary>The whole document, via the clipboard; Ctrl+End afterwards drops the selection.</summary>
    private static string ReadAll()
    {
        Chord('A', Ctrl);
        Chord('C', Ctrl);
        Thread.Sleep(300);
        var text = GetClipboard();
        Chord(0x23, Ctrl);
        Thread.Sleep(100);
        return text.Replace("\r\n", "\n");
    }

    private static void TypeInEnglish(string keys) => TypeIn(Lang.English, keys);

    private static void TypeIn(Lang layout, string keys, int gapMs = 150)
    {
        Layouts.SwitchTo(layout);
        Thread.Sleep(500);

        // Physical keys: letters by name, ';' = the tonos key (VK_OEM_1), ' ' = Space.
        foreach (var c in keys)
        {
            ushort vk = c switch { ' ' => Space, ';' => 0xBA, _ => char.ToUpperInvariant(c) };
            Tap(vk, c == ' ' ? Math.Min(80, gapMs) : 30);
            Thread.Sleep(gapMs);
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

    /// <summary>Where keys may go. Notepad by default; the matrix points it at the app under test.</summary>
    internal static Func<bool> TargetIsForeground { get; set; } = () =>
    {
        _ = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var pid);
        return Process.GetProcessesByName("Notepad").Any(p => p.Id == pid);
    };

    private static bool NotepadIsForeground() => TargetIsForeground();

    /// <summary>The foreground window's title (for the matrix's safety check).</summary>
    internal static string ForegroundTitle()
    {
        var buffer = new StringBuilder(512);
        _ = Native.GetWindowTextW(Native.GetForegroundWindow(), buffer, buffer.Capacity);
        return buffer.ToString();
    }

    internal static void FocusWindowOf(Process process) => _ = Native.SetForegroundWindow(process.MainWindowHandle);

    internal static void Type(Lang layout, string keys, int gapMs) => TypeIn(layout, keys, gapMs);

    internal static string ReadAndClearText() => ReadAndClear();

    internal static void Expect(string name, string actual, string expected) => Check(name, actual, expected);

    internal static int Failures => _failures;

    internal static void ReleaseModifiers()
    {
        foreach (var m in new[] { Ctrl, Alt, Shift })
        {
            _ = Native.SendInput(1, [Key(m, up: true)], Marshal.SizeOf<INPUT>());
        }
    }

    internal static bool IsFocusLost(Exception e) => e is FocusLostException;

    /// <summary>Only log lines written from now on are printed with the next check.</summary>
    internal static void MarkLog() => _logMark = File.ReadAllLines(LogPath).Length;

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

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(nint hwnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern nint SetWindowsHookExW(int id, HookProc proc, nint module, uint threadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll")]
        public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        public static extern int GetMessageW(out MSG msg, nint hwnd, uint min, uint max);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandleW(string? name);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool PostThreadMessageW(uint threadId, uint msg, nint wParam, nint lParam);
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }
}
