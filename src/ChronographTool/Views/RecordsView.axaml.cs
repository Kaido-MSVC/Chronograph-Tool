using Avalonia.Controls;
using Avalonia.Interactivity;
using ChronographTool.ViewModels;

namespace ChronographTool.Views;

public partial class RecordsView : UserControl
{
    public RecordsView()
    {
        InitializeComponent();
        RecordList.SelectionChanged += (_, _) =>
            (DataContext as RecordsViewModel)?.SetSelection(RecordList.SelectedItems?.OfType<RecordItemViewModel>() ?? []);
        // 備註在編輯框失去焦點時寫入檔案。
        NoteBox.LostFocus += (_, _) => (DataContext as RecordsViewModel)?.CommitNote();
    }
}
