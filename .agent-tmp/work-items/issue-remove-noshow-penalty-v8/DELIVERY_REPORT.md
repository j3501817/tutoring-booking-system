# 交付報告 (DELIVERY_REPORT)

## 任務摘要
- **Work Item ID**: `issue-remove-noshow-penalty-v8`
- **Branch**: `feat/remove-noshow-penalty-v8`
- **PM 指示**: 爽約確認過目前不需要停權功能，拔除此功能（因爽約本來就會扣除堂數，無需額外懲罰暫停預約）。

## 變更項目說明
1. **預約防呆解鎖 (`BookingService.cs`)**:
   - 拔除 `ReserveSlotAsync` 中針對團體課爽約停權日期（`GroupBookingSuspendedUntilUtc`）的阻擋限制，學生預約不再受爽約停權阻礙。
2. **點名流程解鎖 (`BookingService.cs`)**:
   - 拔除 `RecordAttendanceAsync` 中點名 NoShow 時的「30 天內爽約 3 次自動暫停預約 14 天」懲罰邏輯。
3. **管理後台簡化 (`Admin/Index.cshtml` & `Index.cshtml.cs`)**:
   - 移除「團體課爽約記點停權（30天內爽約3次暫停預約14天）」的開關設定卡片與對應的 `ToggleNoShowPenalty` 後端 Handler。
   - 相關標題與選單項目簡化為純粹的「到班目的管理」與「學生與到班目的」。
4. **領域邊界強化**:
   - 此更動使學生實體（Student）徹底脫離預約停權狀態的耦合，使學籍主資料更純粹。
5. **單元測試驗證 (`BookingAndLedgerTests.cs`)**:
   - 調整測試案例驗證 NoShow 發生時不觸發停權、學生預約流程順暢無阻。23 項測試全數綠燈通過。

## 驗證結果
- 編譯與建置成功（Build Succeeded）。
- 單元測試：23/23 通過（100%）。
