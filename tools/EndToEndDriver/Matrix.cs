using System.Diagnostics;
using GreekSwitch.Core;

/// <summary>
/// The same typing cases in several apps: Notepad, Word, and three browsers (textarea and
/// contenteditable). Keys are only sent while the foreground window's title carries the test
/// marker; browsers run with a throwaway profile, so the user's own windows are never touched.
/// Word is only used when it isn't already running (it is killed at the end, unsaved).
/// </summary>
internal static class Matrix
{
    private const string Marker = "gs-e2e";

    /// <summary>Physical keys, the layout active when typing starts, and what should be on screen.</summary>
    private static readonly (string Name, Lang Layout, string Keys, string Expected)[] Cases =
    [
        ("English typed in the Greek layout", Lang.Greek, "container ", "container "),
        ("two English words in the Greek layout", Lang.Greek, "route survey ", "route survey "),
        ("Greek typed in the English layout", Lang.English, "doyleyei ", "δουλεύει "),
        ("Greek without accents, Greek layout", Lang.Greek, "elegxoyme pali ama to systhma doyleyei kala ", "ελέγχουμε πάλι άμα το σύστημα δουλεύει καλά "),
        ("mixed sentence started in the English layout", Lang.English, "uelv na ftiajv ena application poy ua kanei prajeiw ", "θέλω να φτιάξω ένα application που θα κάνει πράξεις "),
    ];

    /// <param name="ProcessName">The app's process: a window of its own that takes focus (an extension page,
    /// a start-up prompt) is the app's business, not the user's, so the case is retried.</param>
    private sealed record Target(string Name, string ProcessName, Func<Process?> Start, Func<string?> Refuse);

    /// <param name="only">Names to run (e.g. "Chrome"), matched against the start of each target's name; empty runs all.</param>
    public static int Run(string workDir, string[] only)
    {
        if (MarkerWindows().Count > 0)
        {
            Console.Error.WriteLine($"A window with \"{Marker}\" in its title is already open (a previous run?): close it first.");
            return 2;
        }

        Directory.CreateDirectory(workDir);
        var text = Path.Combine(workDir, Marker + ".txt");
        var rtf = Path.Combine(workDir, Marker + ".rtf");
        var textarea = Path.Combine(workDir, Marker + "-textarea.html");
        var editable = Path.Combine(workDir, Marker + "-editable.html");
        File.WriteAllText(text, "");
        File.WriteAllText(rtf, "{\\rtf1\\ansi }");
        File.WriteAllText(textarea, $"<!doctype html><meta charset=utf-8><title>{Marker} textarea</title><textarea autofocus style='width:95%;height:80vh;font-size:20px'></textarea>");
        File.WriteAllText(editable, $"<!doctype html><meta charset=utf-8><title>{Marker} editable</title><div contenteditable style='width:95%;height:80vh;font-size:20px;border:1px solid' id=e></div><script>document.getElementById('e').focus()</script>");

        var word = Find(@"Microsoft Office\Root\Office16\WINWORD.EXE");
        var edge = Find(@"Microsoft\Edge\Application\msedge.exe");
        var chrome = Find(@"Google\Chrome\Application\chrome.exe");
        var firefox = Find(@"Mozilla Firefox\firefox.exe");
        var wordcraft = Find(@"WordCraft\wordcraft.exe");
        var docx = Path.Combine(workDir, Marker + ".docx");
        const string BrowserFlags = "--no-first-run --no-default-browser-check --disable-extensions --new-window";
        string Profile(string name) => Directory.CreateDirectory(Path.Combine(workDir, "profile-" + name)).FullName;

        Target[] targets =
        [
            new("Notepad", "notepad", () => Process.Start("notepad.exe", Quote(text)), () => null),
            new("Word", "WINWORD", () => Process.Start(word!, Quote(rtf)),
                () => word is null ? "not installed"
                    : Process.GetProcessesByName("WINWORD").Length > 0 ? "Word is already open: close it to include Word" : null),
            new("Edge, textarea", "msedge", () => Process.Start(edge!, $"--user-data-dir={Quote(Profile("edge"))} {BrowserFlags} {Quote(textarea)}"), () => edge is null ? "not installed" : null),
            new("Chrome, contenteditable", "chrome", () => Process.Start(chrome!, $"--user-data-dir={Quote(Profile("chrome"))} {BrowserFlags} {Quote(editable)}"), () => chrome is null ? "not installed" : null),
            new("Firefox, textarea", "firefox", () => Process.Start(firefox!, $"-no-remote -profile {Quote(Profile("firefox"))} {Quote(textarea)}"), () => firefox is null ? "not installed" : null),
            new("WordCraft", "wordcraft", () => Process.Start(wordcraft!, Quote(NewDocx(wordcraft!, rtf, docx))),
                () => wordcraft is null ? "not installed"
                    : Process.GetProcessesByName("wordcraft").Length > 0 ? "WordCraft is already open: close it to include it" : null),
        ];

        var summary = new List<string>();
        var stopped = false;
        foreach (var target in targets)
        {
            if (only.Length > 0 && !only.Any(o => target.Name.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (target.Refuse() is { } reason)
            {
                summary.Add($"{target.Name}: skipped ({reason})");
                continue;
            }

            Console.WriteLine($"===== {target.Name}");
            var failuresBefore = Program.Failures;
            Process? process = null;
            try
            {
                process = target.Start();
                if (!WaitForMarkerInFront(TimeSpan.FromSeconds(25)))
                {
                    summary.Add($"{target.Name}: skipped (its window didn't come to the front)");
                    continue;
                }

                Program.TargetIsForeground = () => Program.ForegroundTitle().Contains(Marker, StringComparison.Ordinal);
                Thread.Sleep(1500);
                Program.MarkLog();
                foreach (var (name, layout, keys, expected) in Cases)
                {
                    string actual;
                    try
                    {
                        actual = TypeCase(layout, keys);
                    }
                    catch (Exception e) when (Program.IsFocusLost(e) && OwnWindowInFront(target))
                    {
                        // The app's own window took focus: bring the test window back, start the case again.
                        Console.WriteLine($"    ({target.Name}'s own window [{Program.ForegroundTitle()}] took focus; retrying the case)");
                        Program.ReleaseModifiers();
                        if (!WaitForMarkerInFront(TimeSpan.FromSeconds(15)))
                        {
                            throw;
                        }

                        _ = Program.ReadAndClearText();
                        actual = TypeCase(layout, keys);
                    }

                    // Each case starts an empty document: word processors capitalise the first word.
                    var want = target.Name is "Word" or "WordCraft" ? char.ToUpperInvariant(expected[0]) + expected[1..] : expected;
                    Program.Expect($"{target.Name}: {name}", actual, want.TrimEnd());
                }

                summary.Add($"{target.Name}: {Cases.Length - (Program.Failures - failuresBefore)}/{Cases.Length} OK");
            }
            catch (Exception e) when (Program.IsFocusLost(e))
            {
                summary.Add($"{target.Name}: stopped, the test window lost focus (no further keys sent)");
                Console.Error.WriteLine($"Stopped: the test window is no longer in front ([{Program.ForegroundTitle()}] is). No further keys were sent.");
                stopped = true;
                break; // the user may be using the PC: stop the whole run
            }
            finally
            {
                Program.ReleaseModifiers();
                Close(target.Name, process);
                Program.TargetIsForeground = () => false;
            }
        }

        Console.WriteLine();
        Console.WriteLine("===== Summary");
        summary.ForEach(Console.WriteLine);
        return Program.Failures == 0 && !stopped ? 0 : 1; // an interrupted run is not a pass
    }

    private static string TypeCase(Lang layout, string keys)
    {
        Program.Type(layout, keys, 120);
        Thread.Sleep(1500); // a pause: accents are added now
        return Program.ReadAndClearText().Replace("\r\n", "\n").TrimEnd(); // the expected text is trimmed too
    }

    private static bool OwnWindowInFront(Target target) =>
        string.Equals(Program.ForegroundProcessName(), target.ProcessName, StringComparison.OrdinalIgnoreCase);

    private static bool WaitForMarkerInFront(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (Program.ForegroundTitle().Contains(Marker, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowTitle.Contains(Marker, StringComparison.Ordinal))
                    {
                        Program.FocusWindowOf(p);
                    }
                }
                catch (InvalidOperationException)
                {
                    // the process exited meanwhile
                }
            }

            Thread.Sleep(500);
        }

        return false;
    }

    private static void Close(string name, Process? started)
    {
        try
        {
            if (name == "Notepad")
            {
                foreach (var p in Process.GetProcessesByName("Notepad"))
                {
                    p.Kill();
                }
            }
            else if (name == "Word")
            {
                foreach (var p in Process.GetProcessesByName("WINWORD"))
                {
                    p.Kill(); // only ever ours: the run refuses to start Word when it's already open
                }
            }
            else if (name == "WordCraft")
            {
                foreach (var p in Process.GetProcessesByName("wordcraft"))
                {
                    p.Kill(); // only ever ours: the run refuses to start it when it's already open
                }
            }
            else if (started is { HasExited: false })
            {
                started.Kill(entireProcessTree: true); // the throwaway-profile browser only
            }
        }
        catch (InvalidOperationException)
        {
        }

        Thread.Sleep(1500);

        // Firefox hands its window to another process, which outlives the one we started. Only
        // test windows carry the marker, so whatever still shows it is ours to close.
        foreach (var p in MarkerWindows())
        {
            try
            {
                p.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    /// <summary>Processes whose main window carries the test marker.</summary>
    private static List<Process> MarkerWindows() =>
        [.. Process.GetProcesses().Where(p =>
        {
            try
            {
                return p.MainWindowTitle.Contains(Marker, StringComparison.Ordinal);
            }
            catch (InvalidOperationException)
            {
                return false; // exited meanwhile
            }
        })];

    /// <summary>A fresh empty Word document for WordCraft, made from the empty RTF with its own CLI.</summary>
    private static string NewDocx(string wordcraft, string rtf, string docx)
    {
        var cli = Path.Combine(Path.GetDirectoryName(wordcraft)!, "wordcraft-cli.exe");
        using var convert = Process.Start(new ProcessStartInfo(cli) { ArgumentList = { "convert", rtf, docx }, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true })!;
        convert.WaitForExit(30_000);
        return docx;
    }

    private static string Quote(string path) => $"\"{path}\"";

    /// <summary>The app under Program Files or Program Files (x86) (32-bit Office and Chrome), or null.</summary>
    private static string? Find(string relative) =>
        new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Path.Combine(Environment.GetFolderPath(folder), relative))
            .FirstOrDefault(File.Exists);
}
