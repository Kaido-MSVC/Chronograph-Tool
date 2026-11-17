using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ChronographTool.Models;
using ChronographTool.ViewModels;

namespace ChronographTool.Views;

public partial class MainWindow : Window
{
    private PixelPoint normalPosition;
    private Size normalSize;
    private bool spaceHandled;

    // 供 XAML 預覽器使用。
    public MainWindow() : this(new AppSettings()) { }

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        UiServices = new WindowUiServices(this);
        RestoreGeometry(settings);

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        PositionChanged += (_, _) => TrackNormalBounds();
        SizeChanged += (_, _) => TrackNormalBounds();
        Closing += (_, _) => FinishSession();
    }

    public IUiServices UiServices { get; }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    /// <summary>收尾：存備註、進行中的計時、設定與視窗位置。可重複呼叫。</summary>
    public void FinishSession()
    {
        // 備註編輯框以 PropertyChanged 更新 ViewModel，即使還沒離開焦點，
        // Shutdown 內的 CommitNote 也會寫入最後一次輸入。
        ViewModel?.Shutdown(WriteGeometry);
    }

    /// <summary>第二個實體啟動時呼叫：還原並帶到前景。</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm || e.KeyModifiers != KeyModifiers.None || IsTextInputFocused()) return;

        ShortcutAction? action = ShortcutKey(e) switch
        {
            PhysicalKey.Space => ShortcutAction.StartPause,
            PhysicalKey.L => ShortcutAction.Lap,
            PhysicalKey.R => ShortcutAction.Reset,
            _ => null,
        };
        if (action is { } a && vm.HandleShortcut(a))
        {
            e.Handled = true;
            // 空白鍵的放開事件也要攔下，避免觸發目前有焦點的按鈕或核取方塊。
            if (a == ShortcutAction.StartPause) spaceHandled = true;
        }
    }

    /// <summary>
    /// 中文輸入法（如注音）啟用時，字母與空白鍵的 Key 會是 ImeProcessed，
    /// 因此以實體按鍵位置判斷；沒有實體鍵資訊時才退回 Key。
    /// </summary>
    private static PhysicalKey ShortcutKey(KeyEventArgs e) => e.PhysicalKey != PhysicalKey.None
        ? e.PhysicalKey
        : e.Key switch
        {
            Key.Space => PhysicalKey.Space,
            Key.L => PhysicalKey.L,
            Key.R => PhysicalKey.R,
            _ => PhysicalKey.None,
        };

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (ShortcutKey(e) == PhysicalKey.Space && spaceHandled)
        {
            spaceHandled = false;
            e.Handled = true;
        }
    }

    private bool IsTextInputFocused()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        for (var v = focused; v is not null; v = v.GetVisualParent())
        {
            if (v is TextBox or ComboBox or CalendarDatePicker or NumericUpDown) return true;
        }
        return false;
    }

    private void RestoreGeometry(AppSettings s)
    {
        if (s.WindowWidth is { } w && s.WindowHeight is { } h && w >= MinWidth && h >= MinHeight)
        {
            Width = w;
            Height = h;
        }

        if (s.WindowX is { } x && s.WindowY is { } y && IsOnScreen(new PixelPoint(x, y)))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(x, y);
        }

        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        normalPosition = Position;
        normalSize = new Size(Width, Height);
    }

    /// <summary>標題列附近要落在某個螢幕的工作區內，避免螢幕配置改變後視窗跑到看不到的地方。</summary>
    private bool IsOnScreen(PixelPoint topLeft)
    {
        var probe = new PixelPoint(topLeft.X + 80, topLeft.Y + 16);
        return Screens.All.Any(screen => screen.WorkingArea.Contains(probe));
    }

    private void TrackNormalBounds()
    {
        if (WindowState != WindowState.Normal) return;
        normalPosition = Position;
        normalSize = ClientSize;
    }

    private void WriteGeometry(AppSettings s)
    {
        TrackNormalBounds();
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.WindowX = normalPosition.X;
        s.WindowY = normalPosition.Y;
        s.WindowWidth = normalSize.Width;
        s.WindowHeight = normalSize.Height;
    }
}
