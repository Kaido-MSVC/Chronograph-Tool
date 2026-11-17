namespace ChronographTool.ViewModels;

/// <summary>ViewModel 需要的視窗層功能，由 View 實作。</summary>
public interface IUiServices
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>開啟存檔對話方塊；取消時回傳 null。</summary>
    Task<Stream?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension);

    /// <summary>倒數時間到的提醒：視窗閃爍與暫時置頂，必要時播放提示音。</summary>
    void AlertTimeUp(bool playSound);
}
