using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChronographTool.Services;
using ChronographTool.ViewModels;

namespace ChronographTool.Views;

internal sealed class WindowUiServices(Window owner) : IUiServices
{
    private static readonly TimeSpan TopmostDuration = TimeSpan.FromSeconds(4);
    private const int SoundRepeats = 3;
    private static readonly TimeSpan SoundInterval = TimeSpan.FromMilliseconds(1200);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog(title, message, confirmText);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<Stream?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [$"*.{extension}"] }],
        });
        return file is null ? null : await file.OpenWriteAsync();
    }

    public void AlertTimeUp(bool playSound)
    {
        if (playSound)
        {
            for (int i = 0; i < SoundRepeats; i++)
                DispatcherTimer.RunOnce(() => NativeMethods.PlaySystemSound("SystemExclamation"), SoundInterval * i + TimeSpan.FromMilliseconds(1));
        }

        if (owner.WindowState == WindowState.Minimized) owner.WindowState = WindowState.Normal;
        NativeMethods.FlashUntilForeground(owner.TryGetPlatformHandle()?.Handle ?? 0);

        // 暫時置頂，讓提醒出現在其他視窗前面；幾秒後恢復，避免一直擋住其他程式。
        owner.Topmost = true;
        owner.Activate();
        DispatcherTimer.RunOnce(() => owner.Topmost = false, TopmostDuration);
    }
}
