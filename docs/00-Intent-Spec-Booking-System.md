# Intent Spec: 柏拉圖補習班－網頁輔導預約與堂數管理系統

Owner: PM
Last updated: 2026-10-17
Status: Draft / Pending PM Review
Risk label: `risk:high`

此文件用來在實作前收斂需求。若任一區塊缺漏導致無法安全實作，Agent 必須建立 `PM_Review.md` 並停止，不得自行創造需求。此文件以原報價單功能為基礎，並以後續確認的「全面改網頁操作、LINE 僅通知、老師儲值堂數、Azure App Service／Azure SQL／Azure Blob」為優先；未確認細節不視為已定案。

## 1. Goal 目標

建立供家長、老師與管理員在電腦及手機瀏覽器使用的補習班輔導預約管理網站，取代 SimplyBook 的輔導預約流程。家長可管理孩子、查看名額、預約、改期、取消及確認候補；老師負責建立時段、點名、作業追蹤、替學生儲值或調整堂數，並登錄匯款資料與照片／附件。系統提供付費與贈送堂數的分軌管理、候補自動輪替、學期規則、報表及 LINE 通知；不提供 LINE 內操作及線上金流。程式直接部署 Azure App Service，業務資料儲存 Azure SQL，附件儲存 Azure Blob Storage。

### 技術選型（建議，待 PM 核定）

- 開發語言／Runtime：C#、ASP.NET Core .NET 10 LTS。
- UI：Razor Pages + Bootstrap 5 + JavaScript；採 RWD 響應式設計及 Mobile First 行動裝置優先原則；預約月曆可採 FullCalendar（實際授權及版本需確認）。
- ORM：Entity Framework Core 10；登入授權：ASP.NET Core Identity + Role/Policy。
- 部署：Azure App Service (Linux, Code Deployment)，單一 Web App 承載網站與應用程式 API；暫不使用 Docker／ACR。
- 排程：建議 Azure Functions Timer Trigger 處理催促、輪替、學期及對帳工作；通知須有冪等、重試與紀錄機制。
- CI/CD：Azure DevOps Pipelines；環境設定與秘密資訊不提交至程式庫。

## 2. Acceptance Criteria 驗收條件

### A. 身分、角色與學生管理

- [ ] 家長、老師、管理員均可透過響應式網站登入；只能操作獲授權的資源。具體登入識別方式待 PM 確認。
- [ ] 家長可關聯與切換多位孩子，僅看見自身被授權的孩子、預約及堂數；管理員可管理學生、家長、角色和班別。
- [ ] LINE User ID 與家長之綁定僅為通知用途；沒有 LINE LIFF 預約、審核或儲值畫面。

### B. 輔導時段與預約

- [ ] 老師能建立常態／加開時段，設定日期、時間、容納名額、所屬班別及進度；管理員可維護學期與進度。
- [ ] 家長可在網頁月曆看可預約時段和即時剩餘名額，選擇孩子及五種到班目的之一後送出預約；五種目的名稱待確認。
- [ ] 預設一週一進度、僅可預約目前進度，小組指導每進度限 1 堂、團體輔導不限；實際規則待 PM 確認並應可設定。
- [ ] 家長可於同進度內改期；開課前 24 小時（含邊界條件待確認）取消退還原扣抵堂數，逾時取消不退；開課前仍有空位可臨時預約。
- [ ] 滿額可登記候補；釋出名額時透過 LINE 通知並保留 30 分鐘；家長必須回網站確認；逾時自動輪替。不得超賣、重複確認或因併發造成重複扣堂。
- [ ] 老師可代學生預約；系統保存操作人與時間，且不繞過必要的預約限制（特殊覆寫權限待確認）。
- [ ] 家長可於網站查看預約清單及預約結果；「確認」至少涵蓋送出前確認與候補轉正式預約確認，是否需老師核准待 PM 確認。

### C. 堂數與儲值（老師操作）

- [ ] 堂數區分付費及贈送兩種餘額；按規則優先扣贈送堂數；家長可查詢餘額及預約相關紀錄。
- [ ] 老師可替指定學生登錄儲值金額、堂數、匯款日期、帳號末五碼、備註、附件照片／檔案；由老師確認後增加付費堂數，不串接支付閘道或銀行自動核帳。
- [ ] 支援老師發放贈送堂數／券及更正堂數；管理員至少可檢視完整操作與異動紀錄，是否能調整待確認。
- [ ] 每筆堂數增加、扣抵、退還及更正都產生不可直接覆寫的流水帳；更正以新交易沖正／調整，不修改原始歷史。
- [ ] 匯款紀錄與附件、儲值批次及堂數流水帳可追溯關聯；匯款憑證儲存在私有 Azure Blob，SQL 儲存識別碼及必要中繼資料。
- [ ] 學生堂數可匯出 CSV；系統每晚對帳，異常可查詢與處理。
- [ ] 小組指導預設每堂 NT$500、6 堂一組；團體輔導免費。此為原 PDF 預設值，實際價格與可儲值單位待核定。

### D. 老師教務與學期規則

- [ ] 老師依負責範圍看到每日輔導任務並點名，課後可定案；支援出席、缺席與爽約紀錄（狀態值待確認）。
- [ ] 老師可勾選作業不佳學生；系統 LINE 通知家長預約，48 小時仍未預約則二次催促並標示。
- [ ] 每學期預設贈送 3 堂；插班生依入班日至期末剩餘天數比例計算、四捨五入；學期結束贈送堂數歸零並留下可稽核紀錄。
- [ ] 可設定團體課爽約記點（預設關閉）；啟用後預設 30 天內缺席 3 次暫停預約 14 天。
- [ ] 管理員可執行退班結算，依各批儲值當時單價及剩餘付費堂數計算應退金額；退款採線下處理並留存紀錄。

### E. LINE 通知與營運報表

- [ ] LINE 只發送通知；至少涵蓋作業不佳、48 小時再次提醒及候補空位通知，候補連結指向網站且必須驗證登入身分。
- [ ] 推播成功／失敗、重試結果及 LINE 用量可查詢；通知不得直接修改堂數或預約結果。
- [ ] 管理員可查看月報：儲值入帳、預收款、出席率、時段滿座率、到班目的、作業追蹤、LINE 推播用量、退班紀錄。
- [ ] 轉帳截圖及相關上傳附件需依明確保存政策清除；原 PDF 指定轉帳截圖保留 90 天，其他附件的保留期限待 PM 確認。

### F. UI／UX、RWD 與跨裝置相容性

- [ ] 所有已核定的家長、老師及管理員功能均可使用手機、平板及電腦瀏覽器操作，採 RWD 響應式設計及 Mobile First 原則；無須安裝原生手機 App。
- [ ] 手機與電腦版可使用不同的版面和操作組件，但功能、資料驗證、權限及業務規則必須一致；不能因裝置不同而遺漏必要功能。
- [ ] 家長預約優先最佳化手機版：日期選擇、可用時段、剩餘名額、到班目的、送出前確認、改期／取消及候補確認應可直覺操作；電腦版可使用月曆與時段並排，手機版改以直式日期及時段卡片顯示。
- [ ] 老師點名、代預約、堂數儲值與匯款紀錄／照片上傳均可在手機完成；手機表單採單欄排列，支援選取手機相簿或拍照後上傳（瀏覽器／裝置允許時），且可檢視附件預覽、上傳結果與驗證錯誤。
- [ ] 管理員表格、報表及各角色選單在小螢幕應採卡片、可展開明細、適當捲動或摺疊導覽，不得因欄位太多使核心操作無法使用。
- [ ] 表單欄位、標籤、提示、錯誤訊息及主要操作按鈕適合觸控；不依賴滑鼠 hover 才能完成核心操作；調整瀏覽器字級時不得遮蔽重要內容。
- [ ] 以 360px、390px、768px、1280px 為最低版面驗證寬度，檢查主要流程沒有非預期水平捲動、內容截斷、欄位遮擋、按鈕無法點擊或彈窗超出視窗；各斷點可依實際裝置補充。
- [ ] 使用可重複的 Layout、Partial View、View Component／CSS 設計規範，讓頁面色彩、字體、間距、按鈕及版面可調整；畫面設計與視覺稿仍須 PM／使用者核定，不能自行假設品牌風格。

### F. 部署、資料保護與遷移

- [ ] 網站透過 Azure App Service 原生 Runtime 部署（非 Docker）；以環境設定及 Managed Identity 存取 Azure SQL、Blob（正式權限方式待安全審查）。
- [ ] Azure SQL 保存帳號、學生、預約、進度、堂數流水帳、匯款與審核紀錄；Azure Blob 僅存附件二進位內容，Container 不公開。
- [ ] 建置資料遷移與核對流程，將 SimplyBook 可匯出且經確認的資料轉入新系統；遷移範圍、欄位映射、正式切換與回滾策略待確認。
- [ ] 手機／桌機主要操作可用；資料庫約束、交易隔離與 API 權限避免超賣及重複儲值。
- [ ] 保留匯出學生、堂數流水帳與預約紀錄的能力，並具備存取控管、稽核及備份還原方案。

## 3. Non-goals 不做什麼

- 不做：LINE LIFF 內預約、LINE 聊天室內管理、LINE 卡片內儲值審核；LINE 僅用於發送通知與必要的通知帳號綁定。
- 不做：線上金流、信用卡、第三方支付、銀行 API 串接或銀行自動對帳；匯款資料由人員人工登錄及查核。
- 不做：Docker、Azure Container Registry、AKS／Kubernetes 或微服務拆分（除非另行核定）。
- 不做：學生獨立帳號、額外行銷／會員 CRM 等原需求以外功能（除非另行確認）。
- 不做：未經 PM 確認自行決定儲值核准人、登入方式、五種到班目的、價格方案或退款爭議處理規則。

## 4. Constraints & Dependencies 限制與相依

- 技術限制：Azure App Service Code Deployment；Azure SQL；Azure Blob Private Container；建議 C#/.NET 10、EF Core 10、Razor Pages/Bootstrap，實際技術選型待 PM 核准。
- 相依服務：LINE 官方帳號及 Messaging API、Azure 資源／權限、SimplyBook 匯出資料、Azure DevOps、背景排程服務。
- 相容性要求：Chrome、Edge、Safari 等主流桌機及手機瀏覽器；RWD + Mobile First，最低驗證寬度 360／390／768／1280px，手機與桌機可採不同版面但功能一致；API 使用 HTTPS；日期時間以 Asia/Taipei 業務時區解釋並統一儲存 UTC 時間戳。
- 安全限制：老師與管理員權限分離；家長不能查閱其他學生資料／匯款憑證；附件驗證檔案型別與大小；不將金鑰寫入程式碼；操作需記錄稽核軌跡。
- 時程或發布限制：原報價單設定簽約後約 10–12 週上線及 2 週老師試行；此為原商業時程，需 PM 重新確認。資料切換前需核對筆數、餘額與預約狀態。
- 待 PM 確認（Blocking）：(1) 登入及 LINE User ID 綁定方式；(2) 家長確認定義、是否老師核准；(3) 匯款由老師獨立登錄或家長也可上傳；(4) 管理員更正堂數權限；(5) 扣堂發生時間與取消退堂邊界；(6) 五種到班目的；(7) 課程進度與額度規則；(8) SimplyBook 實際資料格式；(9) 候補順位與失敗補償；(10) 退費批次計價／未到場規則；(11) 附件格式、大小與保存期限；(12) 單一 Web App 與排程服務實際 SKU、環境與部署權限。

## 5. Change Contract Mapping 變更契約

| 問題 | Y/N/N/A | 說明 |
|---|---|---|
| 是否變更 API endpoint、request 或 response？ | Y | 新建預約、候補、堂數、匯款、附件、點名、報表等應用程式接口；需 API contract。 |
| 是否變更資料庫 schema 或 migration？ | Y | Azure SQL 新建 schema；SimplyBook 遷移映射；需 schema／migration spec。 |
| 是否變更 authentication / authorization / permission model？ | Y | 家長、老師、管理員資料隔離及權限政策；需 auth spec。 |
| 是否涉及 secrets、金流、資安或 production 設定？ | Y | 無線上金流，但有人工匯款紀錄、個資、Blob、LINE token、正式環境資源與機敏設定。 |
| 是否會刪除資料或執行 destructive operation？ | Y | 贈送堂數學期歸零、附件 90 天刪除及合約終止刪除；需保留與核准程序。 |
| 是否影響多個 app、service 或 shared library？ | Y | Web App、Azure SQL、Blob、LINE、排程及資料遷移。 |

**因有多項 Y，需補充資料模型／ERD、API schema、授權矩陣、排程契約、資料遷移計畫，並送 PM 人工審查。未通過前不得逕行實作高風險部分。**

## 6. Validation Plan 驗證計畫

- Lint：`dotnet format --verify-no-changes`；前端 JavaScript 依實際框架加入 lint。
- Typecheck：`dotnet build`（C# 編譯型別檢查）；若使用 TypeScript 另執行 `tsc --noEmit`。
- Unit test：堂數扣抵優先序、退款批次計價、學期贈送比例、24 小時取消界線、48 小時催促、30 分鐘候補逾時及爽約記點。
- Integration test：Azure SQL 交易與並發名額、預約／取消／改期、儲值流水帳一致性、Blob 上傳與權限拒絕、LINE 通知失敗重試、排程冪等。
- Build：`dotnet restore`、`dotnet build --configuration Release`、`dotnet test --configuration Release`、`dotnet publish --configuration Release`；Dev／UAT 發布後健康檢查。
- Manual check：以 360／390／768／1280px 及 Chrome、Edge、Safari 驗證 RWD；檢查無非預期水平捲動、內容遮擋、操作按鈕失效、觸控困難；對照電腦與手機功能一致；實測家長手機預約／改期／候補確認、老師手機點名／登錄儲值／拍照或上傳匯款附件、管理員行動版查詢報表；並確認權限隔離、資料匯出、SimplyBook 遷移對帳、錯誤畫面及時區。
- Security：測試水平越權、檔案上傳、CSRF、敏感資料外洩、連線字串／LINE token 配置、保留期限及可稽核性。

## 7. Risk Review 風險檢查

**必須標記 `risk:high` 並進入 PM Review。**

- [x] authentication / authorization：三類角色，家長與學生資料隔離。
- [x] payment / billing：人工匯款登錄、付費堂數與退費計算（雖不含線上金流）。
- [x] security / secrets：LINE token、SQL、Blob、個資及附件。
- [x] database migration：SimplyBook 遷移、帳務核對及回滾。
- [x] production deployment：Azure App Service 正式部署與服務切換。
- [x] infrastructure / Terraform / Kubernetes：Azure 資源建立及權限配置；不使用 Kubernetes。
- [x] permission model：老師操作儲值、家長確認預約、管理員審查權限。
- [x] data deletion：附件到期清除、學期贈送堂數歸零、合約終止刪除。
- [x] destructive operation：資料遷移覆寫／清理需事前備份與審核。

## 8. Version Log 版本紀錄

- v0.1.0 - 初版：依原報價單整理功能 - 2026-10-17 - PM
- v0.2.0 - 改為全網頁操作、LINE 僅通知、老師儲值堂數、人工匯款附件 - 2026-10-17 - PM
- v0.3.0 - 指定 Azure App Service／Azure SQL／Blob，建議 C# ASP.NET Core .NET 10 與 Code Deployment - 2026-10-17 - PM
- v0.4.0 - 新增 RWD／Mobile First、手機及電腦差異化排版、觸控與附件操作及跨裝置驗收標準 - 2026-10-17 - PM

## Agent Gate Check

Agent 在開始實作前必須確認：

- [x] Goal 是否明確。
- [x] Acceptance Criteria 是否可驗證（未決條件已標記）。
- [x] Non-goals 是否足以限制 scope。
- [x] 高風險項目是否已標示。
- [x] 驗證方式是否可執行或可替代。
- [x] 若需求不足，是否已建立 `PM_Review.md` 並停止（已建立；阻擋項確認前不開始實作）。
