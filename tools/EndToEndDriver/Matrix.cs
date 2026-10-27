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

    private sealed record Target(string Name, Func<Process?> Start, Func<string?> Refuse);

    public static int Run(string workDir)
    {
        Directory.CreateDirectory(workDir);
        var text = Path.Combine(workDir, Marker + ".txt");
        var rtf = Path.Combine(workDir, Marker + ".rtf");
        var textarea = Path.Combine(workDir, Marker + "-textarea.html");
        var editable = Path.Combine(workDir, Marker + "-editable.html");
        File.WriteAllText(text, "");
        File.WriteAllText(rtf, "{\\rtf1\\ansi }");
        File.WriteAllText(textarea, $"<!doctype html><meta charset=utf-8><title>{Marker} textarea</title><textarea autofocus style='width:95%;height:80vh;font-size:20px'></textarea>");
        File.WriteAllText(editable, $"<!doctype html><meta charset=utf-8><title>{Marker} editable</title><div contenteditable style='width:95%;height:80vh;font-size:20px;border:1px solid' id=e></div><script>document.getElementById('e').focus()</script>");

        var word = @"C:\Program Files\Microsoft Office\Root\Office16\WINWORD.EXE";
        var edge = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe");
        var chrome = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe");
        var firefox = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Mozilla Firefox\firefox.exe");
        string Profile(string name) => Directory.CreateDirectory(Path.Combine(workDir, "profile-" + name)).FullName;

        Target[] targets =
        [
            new("Notepad", () => Process.Start("notepad.exe", Quote(text)), () => null),
            new("Word", () => Process.Start(word, Quote(rtf)),
                () => Process.GetProcessesByName("WINWORD").Length > 0 ? "Word is already open: close it to include Word" : null),
            new("Edge, textarea", () => Process.Start(edge, $"--user-data-dir={Quote(Profile("edge"))} --no-first-run --no-default-browser-check --new-window {Quote(textarea)}"), () => null),
            new("Chrome, contenteditable", () => Process.Start(chrome, $"--user-data-dir={Quote(Profile("chrome"))} --no-first-run --no-default-browser-check --new-window {Quote(editable)}"), () => null),
            new("Firefox, textarea", () => Process.Start(firefox, $"-no-remote -profile {Quote(Profile("firefox"))} {Quote(textarea)}"), () => null),
        ];

        var summary = new List<string>();
        foreach (var target in targets)
        {
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
                    Program.Type(layout, keys, 120);
                    Thread.Sleep(1500); // a pause: accents are added now
                    var actual = Program.ReadAndClearText().Replace("\r\n", "\n").TrimEnd('\r', '\n');
                    Program.Expect($"{target.Name}: {name}", actual, expected.TrimEnd());
                }

                summary.Add($"{target.Name}: {Cases.Length - (Program.Failures - failuresBefore)}/{Cases.Length} OK");
            }
            catch (Exception e) when (Program.IsFocusLost(e))
            {
                summary.Add($"{target.Name}: stopped, the test window lost focus (no further keys sent)");
                Console.Error.WriteLine("Stopped: the test window is no longer in front. No further keys were sent.");
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
        return Program.Failures == 0 ? 0 : 1;
    }

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
            else if (started is { HasExited: false })
            {
                started.Kill(entireProcessTree: true); // the throwaway-profile browser only
            }
        }
        catch (InvalidOperationException)
        {
        }

        Thread.Sleep(1500);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
