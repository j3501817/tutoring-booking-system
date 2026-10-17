// scripts/notify-pm.js
try { require('dotenv').config({ override: true }); } catch (e) {}

// 讀取通知策略，預設為 NONE 以防誤觸發
const strategy = (process.env.NOTIFICATION_STRATEGY || 'NONE').toUpperCase();

// 接收 Agent 傳來的動態工作單編號 (例如: REQ-20260508-001)
const workItemId = process.argv[2];

if (!workItemId) {
    console.error("❌ 缺少工作單編號。用法：node scripts/notify-pm.js <work-item-id>");
    process.exit(1);
}

if (!/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(workItemId)) {
    console.error(`❌ 工作單編號格式不合法：${workItemId}`);
    process.exit(1);
}

let warningMsg = "⚠️ 【Agentic 團隊回報】Agent 遇到重大衝突已暫停，請立即決策！";

// 若有傳入工作單編號，動態組合路徑並提示準確的審查位置
warningMsg = `⚠️ 【工作單 ${workItemId} 阻礙回報】\nAgent 觸發停損機制或重大衝突，請查看 .agent-tmp/work-items/${workItemId}/PM_Review.md 進行決策！`;

// --- LINE 推播模組 ---
async function sendLine() {
    const token = process.env.LINE_CHANNEL_ACCESS_TOKEN;
    const userId = process.env.LINE_USER_ID;
    if (!token || !userId) {
        console.error("❌ [LINE] 金鑰或 UserID 缺失，跳過發送。");
        return;
    }
    
    const payload = { to: userId, messages: [{ type: "text", text: warningMsg }] };
    try {
        const res = await fetch('https://api.line.me/v2/bot/message/push', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(payload)
        });
        if (res.ok) console.log(`✅ [LINE] 工作單 ${workItemId || '全域'} 推播成功。`);
        else console.error("❌ [LINE] 推播失敗，HTTP 狀態碼:", res.status);
    } catch (err) { console.error("❌ [LINE] API 請求發生錯誤:", err); }
}

// --- Microsoft Power Automate (Adaptive Card) 推播模組 ---
async function sendMPA() {
    const webhookUrl = process.env.MPA_WEBHOOK_URL;
    if (!webhookUrl) {
        console.error("❌ [MPA] Webhook URL 缺失，跳過發送。");
        return;
    }
    
    const payload = {
        type: "message",
        attachments: [{
            contentType: "application/vnd.microsoft.card.adaptive",
            content: {
                $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
                type: "AdaptiveCard",
                version: "1.2",
                body: [{ type: "TextBlock", text: warningMsg, wrap: true, weight: "bolder", color: "attention" }]
            }
        }]
    };

    try {
        const res = await fetch(webhookUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        if (res.ok) console.log(`✅ [MPA] 工作單 ${workItemId || '全域'} Adaptive Card 觸發成功。`);
        else console.error("❌ [MPA] 觸發失敗，HTTP 狀態碼:", res.status);
    } catch (err) { console.error("❌ [MPA] Webhook 請求發生錯誤:", err); }
}

// --- 總控中心 ---
async function main() {
    console.log(`啟動通知模組... 當前策略: ${strategy}`);
    const tasks = [];
    
    if (strategy === 'LINE' || strategy === 'BOTH') tasks.push(sendLine());
    if (strategy === 'MPA' || strategy === 'BOTH') tasks.push(sendMPA());
    
    if (tasks.length === 0) {
        console.warn("⚠️ 未啟用任何有效的通知策略 (LINE/MPA/BOTH)，或策略設為 NONE。");
        return;
    }
    
    // 平行執行所有推播任務
    await Promise.allSettled(tasks);
}

main();
