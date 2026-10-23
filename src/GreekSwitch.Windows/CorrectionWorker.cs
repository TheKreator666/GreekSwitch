using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Hooks;
using GreekSwitch.Windows.Layouts;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows;

/// <summary>
/// Consumes hook events on a worker task and drives the <see cref="TypingSession"/>. It decides
/// when to be fully passive (disabled, paused, excluded app, password field) and then tells the
/// hook to stop capturing keys at all. A failure while processing one event is logged (type and
/// stack trace, never text), the buffer is reset, and the worker carries on.
/// </summary>
public sealed class CorrectionWorker
{
    private readonly InputMonitor _monitor;
    private readonly TypingSession _session;
    private readonly DecisionEngine _engine;
    private readonly ILayoutSwitcher _layouts;
    private readonly IPasswordFieldDetector _passwords;
    private readonly ICorrectionPolicy _policy;
    private readonly ILogger _logger;
    private bool _excluded;
    private bool _passwordField;

    public CorrectionWorker(
        InputMonitor monitor,
        TypingSession session,
        DecisionEngine engine,
        ILayoutSwitcher layouts,
        IPasswordFieldDetector passwords,
        ICorrectionPolicy policy,
        ILogger logger)
    {
        _monitor = monitor;
        _session = session;
        _engine = engine;
        _layouts = layouts;
        _passwords = passwords;
        _policy = policy;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        OnForegroundChanged(GetForegroundWindow());
        await foreach (var input in _monitor.Events.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                Handle(input);
            }
#pragma warning disable CA1031 // keep the pipeline alive; the failure is logged, not swallowed
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError("Pipeline failed: {Type}{NewLine}{Stack}", ex.GetType().FullName, Environment.NewLine, ex.StackTrace);
                _session.Reset();
            }
        }
    }

    private void Handle(InputEvent input)
    {
        switch (input.Kind)
        {
            case InputEventKind.Key:
                OnKey(input);
                break;
            case InputEventKind.MouseClick:
                _session.Reset();
                break;
            case InputEventKind.ForegroundChanged:
                OnForegroundChanged(input.Window);
                break;
            case InputEventKind.FocusChanged:
                OnFocusChanged();
                break;
            case InputEventKind.PolicyChanged:
                _session.Reset();
                UpdateCapture();
                break;
            case InputEventKind.HookError:
                _logger.LogError("Keyboard hook callback failed: {Type}", input.ErrorType);
                _session.Reset();
                break;
        }
    }

    private void OnKey(InputEvent input)
    {
        _monitor.TypeAhead.MarkProcessed(input.TypedCount);
        _monitor.Focus.Expect(input.Window);

        // Keys queued just before the hook was told to stop capturing are dropped here.
        if (!_monitor.CaptureKeys)
        {
            return;
        }

        // The layout when the key was pressed (read in the hook), not when the worker got to it.
        if (input.KeyLanguage(_layouts.CurrentLayout) is not { } layout)
        {
            _session.Reset(); // a third layout is active: stay out of the way
            return;
        }

        _session.OnKey(input.Key, layout);
    }

    private void OnForegroundChanged(nint window)
    {
        _session.Reset();
        _engine.ClearContext();
        var process = ProcessNames.FromWindow(window);
        _excluded = process is not null && _policy.IsExcluded(process);
        _passwordField = !_excluded && _passwords.IsPasswordFieldFocused();
        UpdateCapture();
        _logger.LogDebug("Foreground changed: excluded {Excluded}, password field {Password}", _excluded, _passwordField);
    }

    private void OnFocusChanged()
    {
        _session.Reset();
        var process = ProcessNames.FromWindow(GetForegroundWindow());
        _excluded = process is not null && _policy.IsExcluded(process);
        _passwordField = !_excluded && _passwords.IsPasswordFieldFocused();
        UpdateCapture();
    }

    private void UpdateCapture()
    {
        var capture = _policy.IsActive && !_excluded && !_passwordField;
        if (capture != _monitor.CaptureKeys)
        {
            _monitor.CaptureKeys = capture;

            // Categories only: never the app name or any text.
            var reason = capture ? "" : !_policy.IsActive ? ": disabled or paused" : _excluded ? ": excluded app" : ": password field";
            _logger.LogInformation("Key capture {State}{Reason}", capture ? "on" : "off (passive)", reason);
        }

        if (!capture)
        {
            _session.Reset();
        }
    }
}
