# Chronograph Tool

Windows 桌面用的碼表與倒數計時器，能自動記錄每次計時，並匯出成 CSV 或 JSON。介面為繁體中文，淺色／深色主題跟隨系統設定。

## 功能

- **碼表**：開始、暫停、繼續、重置、計次。顯示格式為 `HH:MM:SS.mmm`；計次清單列出編號、單圈時間、累計時間，並標示最快與最慢的一圈（兩圈以上才標示）。
- **計時器（倒數）**：可設定時、分、秒，提供 1／3／5／10／25 分鐘預設。時間到時播放 Windows 系統音效（可以關閉），工作列閃爍，視窗暫時置頂約 4 秒。
- **紀錄**：碼表重置、倒數完成或中途重置時，自動存一筆紀錄。內容包含類型、狀態（完成／中止）、開始與結束時間、總時長、設定時長、計次明細和可編輯的備註。**總時長少於 1 秒的不存**。
- **紀錄頁**：可依類型和日期區間篩選。按 Ctrl／Shift 可多選；刪除前會先確認。備註在編輯框失去焦點、切換紀錄或關閉程式時自動存檔。
- **匯出**：範圍可選全部紀錄、選取的紀錄或目前篩選結果，格式有三種：
  - CSV（每筆紀錄一列，計次合併成一欄）
  - CSV（計次明細，每圈一列：紀錄 ID、開始時間、圈號、單圈時間、累計時間）
  - JSON

  CSV 為 UTF-8 with BOM，用 Excel 開啟中文不會亂碼；欄位依 RFC 4180 跳脫，換行為 CRLF。
- **快捷鍵**（只在碼表與計時器頁有效，焦點在輸入框時不攔截）：`空白鍵` 開始／暫停，`L` 計次，`R` 重置。開著注音等中文輸入法時也能用。
- 會記住視窗位置、大小、最大化狀態、目前分頁、倒數設定與提示音開關。

## 資料與防止資料遺失

資料存放在 `%APPDATA%\Chronograph Tool\`：

| 檔案 | 內容 |
|---|---|
| `records.json` | 所有紀錄 |
| `settings.json` | 視窗與偏好設定 |
| `*.bak` | 上一版的備份 |

- **寫入**：每次都先寫入 `.tmp` 並清到磁碟，再用 `File.Replace` 取代原檔，同時把上一版保留成 `.bak`。
- **讀取失敗（JSON 損壞）**：不會用空資料覆蓋。損壞的檔案會改名保留為 `records.corrupt-yyyyMMdd-HHmmss.json`（settings 同理）。能從 `.bak` 還原就先還原，並在視窗頂端提示。
- **無法讀取（被鎖住、權限不足）**：本次執行不會寫入該檔，避免覆蓋原檔。
- **關閉視窗時**：會先存還在編輯的備註；碼表或倒數還在跑（或暫停中）的話，直接存成一筆紀錄，倒數標為「中止」。
- **只能開一個程式**：用具名 Mutex 限制。重複開啟時，會把已開啟的視窗帶到前景後結束，避免兩個程式同時寫入 `records.json`。

## 技術與版本

| 項目 | 版本 |
|---|---|
| .NET SDK | 11.0.100（`global.json` 固定，`rollForward: latestPatch`） |
| 執行階段 | Microsoft.NETCore.App 11.0.0 |
| TargetFramework | `net11.0-windows` |
| C# | 15.0（`LangVersion` 明確設定；SDK 11.0.100 的預設版本，非 preview） |
| Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent | 12.1.6 |
| CommunityToolkit.Mvvm | 8.4.2 |
| xunit.v3（含 xunit.v3.mtp-v2） | 4.0.3 |
| Microsoft.Testing.Platform | 2.5.2（xunit.v3 的相依套件） |

- 架構是 MVVM，ViewModel 使用 CommunityToolkit.Mvvm 的 `[ObservableProperty]`（partial property）與 `[RelayCommand]`。
- 計時使用 `TimeProvider` 的單調時間戳差值（實際上就是 `Stopwatch`），UI 用約 60 fps 的 `DispatcherTimer` 刷新，只負責顯示，不累加時間。倒數完成時刻會扣除偵測延遲，回推成實際到時的時間點。
- JSON 用 System.Text.Json 的 source generator。
- 兩個專案都提交 `packages.lock.json`，`NuGetAudit` 已開啟。

## 專案結構

```
src/ChronographTool/
  Core/        StopwatchEngine、CountdownEngine、TimeFormat（不依賴 UI）
  Models/      SessionRecord、LapRecord、AppSettings
  Services/    JsonFileStore（安全讀寫）、RecordRepository、SettingsService、
               Exporters（CSV／JSON）、SingleInstance、NativeMethods（音效、閃爍）
  ViewModels/  Main / Stopwatch / Countdown / Records
  Views/       MainWindow、三個分頁、ConfirmDialog、WindowUiServices（StorageProvider 等）
tests/ChronographTool.Tests/
  碼表計次、倒數結束判斷、紀錄存讀（含損壞檔保留與 .bak 還原）、CSV 格式與跳脫
```

## 建置與執行

需要 .NET SDK 11.0.100。

```powershell
dotnet restore ChronographTool.slnx --locked-mode
dotnet build ChronographTool.slnx
dotnet test --solution ChronographTool.slnx
dotnet run --project src/ChronographTool
```

`dotnet test` 透過 `global.json` 的 `test.runner` 使用 Microsoft Testing Platform。從 .NET 10 SDK 起，xUnit v3 不再支援用 VSTest 模式執行 `dotnet test`。

發行版（framework-dependent，需要安裝 .NET 11 Desktop Runtime）：

```powershell
dotnet publish src/ChronographTool -c Release -r win-x64 --self-contained false -o publish
```
