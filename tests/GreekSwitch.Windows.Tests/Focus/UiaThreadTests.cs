using GreekSwitch.Windows.Focus;

namespace GreekSwitch.Windows.Tests.Focus;

public class UiaThreadTests
{
    [Fact]
    public async Task Runs_work_on_its_own_thread_and_returns_the_answer()
    {
        using var uia = new UiaThread();
        var callerThread = Environment.CurrentManagedThreadId;

        var (answered, thread) = await uia.RunAsync(() => Environment.CurrentManagedThreadId, TimeSpan.FromSeconds(5));

        answered.Should().BeTrue();
        thread.Should().NotBe(callerThread);
    }

    [Fact]
    public async Task A_slow_call_times_out_and_later_calls_queue_behind_it()
    {
        using var uia = new UiaThread();
        using var release = new ManualResetEventSlim();

        var slow = await uia.RunAsync(() => release.Wait(TimeSpan.FromSeconds(10)), TimeSpan.FromMilliseconds(50));
        var queued = await uia.RunAsync(() => 42, TimeSpan.FromMilliseconds(50));
        release.Set();

        slow.Answered.Should().BeFalse();
        queued.Answered.Should().BeFalse("the thread is still busy with the hung call");
        (await uia.RunAsync(() => 42, TimeSpan.FromSeconds(5))).Should().Be((true, 42));
    }

    [Fact]
    public async Task An_exception_counts_as_no_answer()
    {
        using var uia = new UiaThread();

        var result = await uia.RunAsync<int>(() => throw new InvalidOperationException(), TimeSpan.FromSeconds(5));

        result.Answered.Should().BeFalse();
    }
}
