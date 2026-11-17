using System.Runtime.InteropServices;

namespace ChronographTool.Services;

internal static partial class NativeMethods
{
    private const uint SndAsync = 0x0001;
    private const uint SndAlias = 0x0001_0000;

    private const uint FlashwAll = 0x3;
    private const uint FlashwTimerNoForeground = 0xC;

    /// <summary>非同步播放 Windows 系統音效（依使用者的音效配置）。</summary>
    public static void PlaySystemSound(string alias) => PlaySound(alias, 0, SndAsync | SndAlias);

    /// <summary>閃爍工作列按鈕與標題列，直到視窗回到前景。</summary>
    public static void FlashUntilForeground(nint hwnd)
    {
        if (hwnd == 0) return;
        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Hwnd = hwnd,
            Flags = FlashwAll | FlashwTimerNoForeground,
            Count = uint.MaxValue,
            Timeout = 0,
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public nint Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(string sound, nint module, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashWindowInfo info);
}
