# Chronograph Tool

Windows 桌面用的碼表與倒數計時器，能自動記錄每次計時，並匯出成 CSV 或 JSON。介面為繁體中文，淺色／深色主題跟隨系統設定。

## 下載

1. 先安裝 [.NET 11 Desktop Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/11.0)：在下載頁的「.NET Desktop Runtime」那一列，下載 Windows x64 安裝程式。已經裝過就不用再裝。
2. 到 [最新版的 Release 頁面](https://github.com/Kaido-MSVC/Chronograph-Tool/releases/latest) 下載 `ChronographTool-<版本>-win-x64.zip`，並請比對該版發行說明裡的 SHA-256。
3. 解壓縮後執行資料夾裡的 `ChronographTool.exe`。整個資料夾要一起保留，不要只把 exe 複製出去。

系統需求：Windows 10／11 x64。紀錄存在 `%APPDATA%\Chronograph Tool\`。

## 功能

- **碼表**：開始、暫停、繼續、重置、計次。顯示格式為 `HH:MM:SS.mmm`；計次清單列出編號、單圈時間、累計時間，並標示最快與最慢的一圈（兩圈以上才標示）。
- **計時器（倒數）**：可設定時、分、秒，提供 1／3／5／10／25 分鐘預設。時間到時播放 Windows 系統音效（可以關閉），工作列閃爍，視窗暫時置頂約 4 秒。
- **紀錄**：碼表重置、倒數完成或中途重置時，自動存一筆紀錄。內容包含類型、狀態（完成／中止）、開始與結束時間、總時長、設定時長、計次明細和可編輯的備註。**總時長少於 1 秒的不存**。
- **紀錄頁**：可依類型和日期區間篩選。按 Ctrl／Shift 可多選；刪除前會先確認。備註在編輯框失去焦點、切換紀錄或關閉程式時自動存檔。
- **匯出**：範圍可選全部紀錄、選取的紀錄或目前篩選結果，格式有三種：
  - CSV（每筆紀錄一列，計次合併成一欄）
  - CSV（計次明細，每圈一列：紀錄 ID、開始時間、圈號、單圈時間、累計時間）
  - JSON

  CSV 為 UTF-8 with BOM，用 Excel 開啟中文不會亂碼；欄位依 RFC 4180 跳脫，換行為 CRLF。以 `=`、`+`、`-`、`@`、Tab 或 CR 開頭的欄位（例如備註）會在前面加上單引號，避免試算表把它當成公式執行；Excel 中不會顯示這個單引號。JSON 匯出保留原文。
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
- **讀取失敗（JSON 損壞或結構不符）**：不會用空資料覆蓋。除了無法解析，像 `"records": null`、`"laps": null` 這類能解析但結構不對的內容也視為損壞。損壞的檔案會改名保留為 `records.corrupt-yyyyMMdd-HHmmss.json`（settings 同理）。能從 `.bak` 還原就先還原，並在視窗頂端提示。
- **主檔不見但有 `.bak`**（例如損壞檔已改名、還原前程式中斷）：從 `.bak` 還原並提示；`.bak` 也損壞時改名保留為 `records.bak.corrupt-yyyyMMdd-HHmmss.json`。
- **無法讀取（被鎖住、權限不足）**：本次執行不會寫入該檔，避免覆蓋原檔。主檔損壞、需要還原，但 `.bak` 暫時讀不到時也一樣：本次不寫入，避免之後的存檔把可能完好的備份輪替掉；解除鎖定後重新開啟程式，就會從 `.bak` 還原。
- **寫入失敗**：資料留在程式中並標記為「未儲存」，下次變更或關閉程式時會再試一次。
- **關閉視窗時**：會先存還在編輯的備註；碼表或倒數還在跑（或暫停中）的話，直接存成一筆紀錄，倒數標為「中止」（已到時但畫面還沒更新的，記為「完成」）。如果紀錄仍然寫不進 `records.json`，會先取消關閉，讓你選擇：
  - **重試**：例如關掉鎖住檔案的程式後再試。
  - **另存新檔…**：把全部紀錄存成 JSON（格式與 `records.json` 相同），之後可改名成 `records.json` 放回資料夾還原。
  - **不儲存並關閉**／**返回程式**。
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

發行版（framework-dependent，需要安裝 [.NET 11 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/11.0)）：

```powershell
dotnet publish src/ChronographTool -c Release -r win-x64 --self-contained false -o publish
```

輸出在 `publish\`，約 27 MB、38 個檔案，執行 `publish\ChronographTool.exe`。分享時請把整個資料夾一起給，不要只複製 exe。發行時會排除 SkiaSharp／HarfBuzz 原生程式庫附帶的 .pdb（約 105 MB），只保留本程式的 `ChronographTool.pdb`。

發行時會一併複製三個文件到 `publish\` 根目錄：`LICENSE`、`THIRD-PARTY-NOTICES.txt`，以及給使用者看的 `使用說明.txt`（原始檔在 `packaging\`）。`LICENSE` 與 `THIRD-PARTY-NOTICES.txt` 另外以內嵌資源打包進程式，分頁列右側的「關於」對話方塊會顯示它們；就算資料夾裡的檔案被刪掉也能看。

## 授權

本專案以 [MIT 授權](LICENSE) 釋出，Copyright (c) 2026 Kaido-MSVC。

發行版包含的第三方元件（Avalonia、CommunityToolkit.Mvvm、SkiaSharp／Skia、HarfBuzzSharp／HarfBuzz、ANGLE、MicroCom.Runtime、Tmds.DBus.Protocol）依各自的授權提供，清單、著作權聲明與授權全文見 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。更新套件版本時，要依 `packages.lock.json` 重新核對這份清單。

## 圖示

`src/ChronographTool/Assets/app.ico` 內含 16–256 px 共 9 種尺寸（PNG 壓縮）。exe 檔案圖示與視窗圖示都用這個檔案。圖示由 `tools/make-icon.ps1` 以 System.Drawing 繪製；要修改時改這個腳本再重新產生：

```powershell
./tools/make-icon.ps1 -OutIco src/ChronographTool/Assets/app.ico
```
