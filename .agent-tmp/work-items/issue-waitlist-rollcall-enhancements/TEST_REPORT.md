# 測試報告：候補排隊整合、點名選項精簡與時段名冊彈窗 (TEST_REPORT.md)

## 1. 偵測技術棧
- **Framework**: .NET 10 (C# / ASP.NET Core Razor Pages)
- **Database**: Entity Framework Core with In-Memory Testing & SQLite/SQL Server Migration
- **Testing Engine**: xUnit Test Runner (`tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj`)

## 2. 測試環境與指令
```bash
DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj --no-restore /p:EnableSourceControlManagerQueries=false
```

## 3. 測試執行結果
- **測試結果**：已通過（Passed）
- **總測試數**：31
- **通過數**：31
- **失敗數**：0
- **略過數**：0
- **持續時間**：684 ms

## 4. 關鍵測試情境驗證
1. **候補取消與後續順位自動重排測試 (`CancelWaitlistAsync_CancelsEntry_ReordersSubsequentEntries_AndLogsHistory`)**：
   - 建立滿額時段，加入 3 位學生依序候補（順位 #1, #2, #3）。
   - 取消順位 #2 學生，驗證其狀態變更為 `Cancelled`、記錄 `CancelledAtUtc` 與原因。
   - 驗證順位 #1 維持不變，順位 #3 自動遞補往前為 #2。
   - 驗證系統自動寫入 `LineNotificationLog`（`NotificationType == "WaitlistCancelled"`）歷程記錄。
2. **所有現有核心業務邏輯回歸測試**：
   - 堂數流水帳不可覆寫性、儲值退班結算一律通過。
   - 48 小時作業不佳二次提醒、學生續報狀態防護、上課時間衝突檢核全部通過。
