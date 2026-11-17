using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ChronographTool.Views;

/// <summary>「關於」對話方塊：程式名稱、版本、著作權、授權，以及檢視授權全文的入口。</summary>
internal sealed class AboutDialog : Window
{
    private const string LicenseName = "MIT 授權";

    public AboutDialog()
    {
        Title = "關於 Chronograph Tool";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var assembly = typeof(AboutDialog).Assembly;
        string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Chronograph Tool";
        string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

        var license = MakeButton("檢視授權");
        var notices = MakeButton("第三方授權");
        var close = MakeButton("關閉");
        close.Classes.Add("accent");
        close.IsCancel = true;
        license.Click += (_, _) => ShowText("授權條款（LICENSE）", "ChronographTool.LICENSE");
        notices.Click += (_, _) => ShowText("第三方授權（THIRD-PARTY-NOTICES.txt）", "ChronographTool.THIRD-PARTY-NOTICES.txt");
        close.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = product, FontSize = 20, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = $"版本 {GetVersion(assembly)}", Classes = { "muted" } },
                new TextBlock { Text = copyright, Margin = new Thickness(0, 12, 0, 0) },
                new TextBlock { Text = $"本程式以 {LicenseName}釋出。", TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = "內含的第三方元件依各自的授權提供。",
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "muted" },
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
                    Margin = new Thickness(0, 20, 0, 0),
                    Children = { license, notices, close },
                },
            },
        };
        Grid.SetColumn(notices, 1);
        Grid.SetColumn(close, 3);
        notices.Margin = new Thickness(8, 0, 0, 0);

        Opened += (_, _) => close.Focus(NavigationMethod.Tab);
    }

    private static Button MakeButton(string text) =>
        new() { Content = text, MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>AssemblyInformationalVersion，去掉 SDK 附加的「+原始碼版本」。</summary>
    private static string GetVersion(Assembly assembly)
    {
        string? info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            int plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return assembly.GetName().Version?.ToString(3) ?? "";
    }

    private void ShowText(string title, string resourceName)
    {
        _ = new LicenseTextDialog(title, ReadResource(resourceName)).ShowDialog(this);
    }

    internal static string ReadResource(string name)
    {
        using var stream = typeof(AboutDialog).Assembly.GetManifestResourceStream(name);
        if (stream is null) return $"找不到內嵌的授權文件：{name}";
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        // 部分授權原文（如 LGPL）含換頁字元，文字框會顯示成方塊，顯示時移除；檔案本身保持原文。
        return reader.ReadToEnd().Replace("\f", "");
    }
}

/// <summary>以可捲動的唯讀文字框顯示授權全文。</summary>
internal sealed class LicenseTextDialog : Window
{
    public LicenseTextDialog() : this("", "") { }

    public LicenseTextDialog(string title, string text)
    {
        Title = title;
        // 寬度容納 SPDX 版 MIT 條文的長行（約 105 字元），避免一行斷成兩行。
        Width = 920;
        Height = 620;
        MinWidth = 480;
        MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        };
        if (Application.Current?.TryFindResource("DigitsFont", out var font) == true && font is FontFamily family)
            box.FontFamily = family;
        ScrollViewer.SetVerticalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

        var close = new Button { Content = "關閉", MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
        close.Classes.Add("accent");
        close.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { close },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Thickness(16),
            Children = { buttons, box },
        };

        // 開啟時游標放在開頭，從第一行開始顯示。
        Opened += (_, _) =>
        {
            box.CaretIndex = 0;
            box.Focus();
        };
    }
}
