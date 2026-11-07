-- =========================================================================
-- 柏拉圖補習班系統資料庫升級腳本
-- 檔案: docs/database/migration_20261104_notifications_and_condition_types.sql
-- 說明: 支援多元學生學習狀況標記 (ConditionType) 與 LINE 官方帳號通知擴充
-- =========================================================================

-- 1. 於 BK_HomeworkWatchItems 增加 ConditionType 欄位
--    （支援：作業不佳、進度落差、觀念不清、輔導未完成、補課確認）
IF OBJECT_ID(N'[dbo].[BK_HomeworkWatchItems]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.BK_HomeworkWatchItems', 'ConditionType') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_HomeworkWatchItems]
        ADD [ConditionType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_BK_HomeworkWatchItems_ConditionType] DEFAULT N'作業不佳';
        PRINT '成功新增欄位 [dbo].[BK_HomeworkWatchItems].[ConditionType]';
    END
    ELSE
    BEGIN
        PRINT '欄位 [dbo].[BK_HomeworkWatchItems].[ConditionType] 已存在，略過。';
    END
END
GO
