using System.Collections.Concurrent;

namespace GreekSwitch.Windows.Focus;

/// <summary>
/// One dedicated (MTA) thread for UI Automation calls, so a slow or hung app can never stall
/// the worker that processes keys. Callers get an answer or a timeout; a call that hangs keeps
/// running on this thread, and later requests queue behind it and time out too (callers then
/// fail closed).
/// </summary>
public sealed class UiaThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public UiaThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "GreekSwitch UI Automation" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    /// <summary>Runs <paramref name="work"/> on the UIA thread; completes with (true, result) or (false, default) on timeout.</summary>
    public async Task<(bool Answered, T? Value)> RunAsync<T>(Func<T> work, TimeSpan timeout)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(() =>
            {
                try
                {
                    completion.TrySetResult(work());
                }
#pragma warning disable CA1031 // handed to the caller through the task
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    completion.TrySetException(ex);
                }
            });
        }
        catch (InvalidOperationException)
        {
            return (false, default); // disposed
        }

        var finished = await Task.WhenAny(completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
        return finished == completion.Task && completion.Task.IsCompletedSuccessfully
            ? (true, completion.Task.Result)
            : (false, default);
    }

    /// <summary>Blocking form of <see cref="RunAsync{T}"/>, for callers that must wait anyway.</summary>
    public (bool Answered, T? Value) Run<T>(Func<T> work, TimeSpan timeout) =>
        RunAsync(work, timeout).GetAwaiter().GetResult();

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(1));
        _queue.Dispose();
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            work();
        }
    }
}
