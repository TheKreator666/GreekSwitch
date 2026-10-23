using System.Threading.Channels;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// Owns the dedicated hook thread: installs the keyboard, mouse and focus hooks there, runs its
/// message loop (low-level hooks and out-of-context WinEvents are delivered through it), and
/// removes every hook when disposed, on process exit, or when the thread ends.
/// </summary>
public sealed class InputMonitor : IDisposable
{
    private readonly Channel<InputEvent> _channel = Channel.CreateUnbounded<InputEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _captureKeys;
    private int _disposed;

    public ChannelReader<InputEvent> Events => _channel.Reader;

    /// <summary>Counts captured typing keys so the corrector can tell when the user has typed ahead.</summary>
    public TypeAheadGuard TypeAhead { get; } = new();

    /// <summary>Modifier and Caps Lock state, for retyping corrections with real keys.</summary>
    public LiveKeyboardState Keyboard { get; } = new();

    /// <summary>
    /// Test mode for end-to-end runs: also process keys injected by *other* programs (our own
    /// output stays ignored). The app turns it on only in Debug builds, behind an environment variable.
    /// </summary>
    public bool AcceptForeignInjectedInput { get; init; }

    /// <summary>
    /// When false the keyboard hook enqueues nothing at all (disabled, paused, password field,
    /// excluded app). Mouse and focus events still flow so the worker can notice changes.
    /// </summary>
    public bool CaptureKeys
    {
        get => _captureKeys;
        set => _captureKeys = value;
    }

    /// <summary>Asks the worker to re-read the policy (enabled, paused, exclusions) from any thread.</summary>
    public void NotifyPolicyChanged() => _channel.Writer.TryWrite(new InputEvent(InputEventKind.PolicyChanged));

    /// <summary>Starts the hook thread and waits until the hooks are installed (or failed).</summary>
    public Task StartAsync()
    {
        if (_thread is not null)
        {
            throw new InvalidOperationException("Already started.");
        }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _thread = new Thread(Run) { IsBackground = true, Name = "GreekSwitch hooks" };
        _thread.Start();
        return _started.Task;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        if (_threadId != 0)
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            _thread?.Join(TimeSpan.FromSeconds(2));
        }

        _channel.Writer.TryComplete();
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    private void Run()
    {
        using var keyboard = new KeyboardHook(_channel.Writer, () => _captureKeys, TypeAhead, Keyboard, AcceptForeignInjectedInput);
        using var mouse = new MouseHook(_channel.Writer);
        using var focus = new FocusWatcher(_channel.Writer);
        try
        {
            _threadId = GetCurrentThreadId();
            keyboard.Install();
            mouse.Install();
            focus.Install();
            _started.TrySetResult();
        }
#pragma warning disable CA1031 // surfaced to the caller of StartAsync
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _started.TrySetException(ex);
            return;
        }

        // The hooks are removed by the using declarations when the loop ends (WM_QUIT or error).
        while (GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(in msg);
            DispatchMessageW(in msg);
        }
    }
}
