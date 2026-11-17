using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace ChronographTool.Views;

/// <summary>簡單的確認對話方塊：確認回傳 true，取消或關閉回傳 false。預設焦點在「取消」。</summary>
internal sealed class ConfirmDialog : Window
{
    public ConfirmDialog() : this("", "", "確定") { }

    public ConfirmDialog(string title, string message, string confirmText)
    {
        Title = title;
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirm = new Button { Content = confirmText, MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        confirm.Classes.Add("accent");
        var cancel = new Button { Content = "取消", MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
        confirm.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { confirm, cancel },
                },
            },
        };

        Opened += (_, _) => cancel.Focus(NavigationMethod.Tab);
    }
}
