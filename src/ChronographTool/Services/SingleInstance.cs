using System.Runtime.InteropServices;

namespace ChronographTool.Services;

/// <summary>
/// 以具名 Mutex 限制同一使用者工作階段只執行一個實體；
/// 第二個實體透過具名事件通知第一個實體把視窗帶到前景後結束。
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\ChronographTool.SingleInstance";
    private const string EventName = @"Local\ChronographTool.Activate";

    private readonly Mutex mutex;
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? registration;

    public bool IsFirst { get; }

    public SingleInstance()
    {
        mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            IsFirst = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // 前一個實體異常結束，仍可接手。
            IsFirst = true;
        }
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    /// <summary>第一個實體：收到喚醒要求時在執行緒集區呼叫 <paramref name="onActivate"/>。</summary>
    public void Listen(Action onActivate) =>
        registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    /// <summary>第二個實體：允許前一個實體搶前景，再發出喚醒訊號。</summary>
    public void ActivateExisting()
    {
        // 目前程序剛由使用者啟動，擁有前景權限，可以轉讓給既有實體。
        AllowSetForegroundWindow(AsfwAny);
        signal.Set();
    }

    public void Dispose()
    {
        registration?.Unregister(null);
        signal.Dispose();
        if (IsFirst) mutex.ReleaseMutex();
        mutex.Dispose();
    }

    private const int AsfwAny = -1;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
