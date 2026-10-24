namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// English terms tier 2 may replace automatically (a strong match on a non-Greek word). The
/// default is office and software vocabulary that Greeks transliterate ad hoc (ντεπλόι, κόμιτ,
/// ρίλιζ). Thread-safe; the list can be replaced (from Settings, later).
/// </summary>
public sealed class DomainVocabulary
{
    /// <summary>The default list: software, IT and office terms.</summary>
    public static readonly IReadOnlyList<string> DefaultTerms =
    [
        // Software development
        "api", "app", "backend", "backup", "branch", "browser", "bug", "build", "cache", "chat", "click",
        "client", "cloud", "cluster", "code", "commit", "compile", "compiler", "computer", "container",
        "cookie", "copy", "crash", "cursor", "dashboard", "data", "database", "debug", "debugger", "default",
        "delete", "deploy", "deployment", "desktop", "developer", "device", "download", "driver", "email",
        "feature", "feedback", "file", "firewall", "folder", "font", "format", "framework", "frontend",
        "hacker", "hardware", "host", "hosting", "icon", "install", "interface", "internet", "keyboard",
        "laptop", "layout", "link", "linux", "login", "logout", "loop", "mail", "merge", "monitor", "mouse",
        "network", "offline", "package", "password", "patch", "pipeline", "plugin", "printer",
        "pull", "push", "query", "refactor", "release", "repository", "request", "reset", "router", "scanner",
        "screen", "screenshot", "script", "scroll", "server", "service", "session", "setup", "site",
        "software", "spam", "sprint", "storage", "stream", "streaming", "string", "switch", "tablet", "tag",
        "template", "terminal", "test", "thread", "ticket", "token", "update", "upgrade", "upload", "user",
        "username", "version", "video", "virus", "web", "website", "widget", "wifi", "window", "windows",
        "workflow",

        // Office and business
        "agenda", "budget", "business", "call", "calendar", "case", "chart", "check", "deadline", "draft",
        "event", "manager", "marketing", "meeting", "online", "presentation", "project", "report", "schedule",
        "slide", "spreadsheet", "startup", "team", "timeline", "training", "webinar", "workshop",
    ];

    private readonly object _gate = new();
    private HashSet<string> _terms;

    public DomainVocabulary(IEnumerable<string>? terms = null)
    {
        _terms = Normalise(terms ?? DefaultTerms);
    }

    public bool Contains(string word)
    {
        var key = word.Trim().ToLowerInvariant();
        lock (_gate)
        {
            return _terms.Contains(key);
        }
    }

    public void ReplaceAll(IEnumerable<string> terms)
    {
        var replacement = Normalise(terms);
        lock (_gate)
        {
            _terms = replacement;
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return [.. _terms.Order(StringComparer.Ordinal)];
        }
    }

    private static HashSet<string> Normalise(IEnumerable<string> terms) =>
        new(terms.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0), StringComparer.Ordinal);
}
