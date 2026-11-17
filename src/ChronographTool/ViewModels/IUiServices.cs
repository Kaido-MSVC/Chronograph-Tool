namespace ChronographTool.ViewModels;

/// <summary>關閉程式時紀錄無法寫入的處理方式。</summary>
public enum UnsavedRecordsChoice
{
    /// <summary>不關閉，回到程式（關閉對話方塊也算）。</summary>
    Cancel,
    Retry,
    SaveCopy,
    Discard,
}

/// <summary>ViewModel 需要的視窗層功能，由 View 實作。</summary>
public interface IUiServices
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>關閉程式時有紀錄沒寫入：詢問要重試、另存、放棄或回到程式。</summary>
    Task<UnsavedRecordsChoice> AskUnsavedRecordsAsync(string message, bool canRetry);

    /// <summary>開啟存檔對話方塊；取消時回傳 null。</summary>
    Task<Stream?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension);

    /// <summary>倒數時間到的提醒：視窗閃爍與暫時置頂，必要時播放提示音。</summary>
    void AlertTimeUp(bool playSound);
}
