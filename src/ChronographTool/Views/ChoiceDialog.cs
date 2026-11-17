using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace ChronographTool.Views;

/// <summary>
/// 多個選項的對話方塊：以 ShowDialog&lt;int?&gt; 取得按下的選項索引；按 Esc 回傳 -1，用標題列關閉回傳 null。
/// 預設焦點在第一個選項。
/// </summary>
internal sealed class ChoiceDialog : Window
{
    public ChoiceDialog() : this("", "", ["確定"]) { }

    public ChoiceDialog(string title, string message, IReadOnlyList<string> choices, int accentIndex = 0)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var button = new Button { Content = choices[i], MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (i == accentIndex) button.Classes.Add("accent");
            button.Click += (_, _) => Close(index);
            buttons.Children.Add(button);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                buttons,
            },
        };

        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close(-1);
        };
        Opened += (_, _) => (buttons.Children.FirstOrDefault() as Button)?.Focus(NavigationMethod.Tab);
    }
}
