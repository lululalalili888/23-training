# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Agent：Claude Code CLI
模型：Claude Sonnet 5（`claude-sonnet-5`）
---

## 通用四問

### 1. 我的任務拆解

練習 1 
官方流程只有三步實際交付的設定內容分四塊：

- 權限清單 `.claude/settings.json`：把 `rm -rf`、`git push --force`、
  `git reset --hard`、改 migration、讀 `appsettings.Production.json`／`*.pfx` 放進
  `deny`；`dotnet ef database drop`、`git push` 放進 `ask`（需要人工確認）；
  `dotnet build`/`test`/`run`、`git status`/`diff`/`log`/`add`/`commit` 放進 `allow`
  （可以直接執行不用每次問）
- 兩個 hook：`block-destructive-sql.ps1` 掛在 `PreToolUse`（攔在 Bash 指令執行前）、
  `log-edits.ps1` 掛在 `PostToolUse`（每次 Edit/Write 後記一筆到 `edit-log.txt`）
- 兩個 subagent：`code-reviewer`（審分層、View 綁定、DataAnnotations 驗證、金額型別、
  測試品質五項）、`test-runner`（跑 `dotnet test`，全綠只回報通過數，有失敗才列斷言訊息）
- 一個 `fix-bug` skill（把練習 2 的六步流程寫死成 skill，之後每次 `/fix-bug` 都照這六步走）

練習 2
官方六步流程走（重現 → 把觀察告訴 agent → 定位根因 → 修復 → 頁面驗證 → 回歸測試）
用 `/fix-bug` skill 逐一處理三個 bug，每個 bug 各一個 commit：

- `da0fd1f fix: 訂單列表第一頁看不到新訂單`
- `bfa82e3 fix: Gold 會員新訂單應付總額被重複打折`
- `141856a fix: 取消訂單後庫存沒有回補，導致庫存持續變少`

練習 3
原本規劃「貼規格 → agent 出計畫 → 我審核 → 核准實作」四步
實際執行時多了兩個沒預期到的分支：

1. 輸入缺漏要先攔下來：第一次貼規格時訊息裡「規格如下」後面其實是空的（貼上的動作
   沒生效），我沒有讓 agent 對著空白規格自己腦補，而是先確認要重貼還是先問關鍵問題，
   確認後才拿到完整規格繼續。
2. 審計畫這件事本身需要換一個獨立視角：agent 出的第一版計畫，我自己重讀了兩輪
   （對照規格逐條核對），但真正抓出問題是在我明確列出五點檢查清單（分層有沒有跑掉、
   有沒有沿用既有慣例、邊界有沒有覆蓋、測試有沒有真的測到規格三件事、有沒有夾帶額外
   重構）之後，讓 agent 另外派一個獨立的審查角色去對照規格原文、`CLAUDE.md` 與實際
   檔案挑計畫的錯，而不是同一個 agent 自己重看同一份計畫——這一步真的挑出了三個可查證
   的問題。

另外，實作階段先跑了兩個平行的 `Explore` agent（一個查 `ProductsController`／
`ProductService`／`Views/Orders/Index.cshtml` 等既有慣例，一個查 `Order`/`OrderItem` 欄位
與 `OrderHubDbContext` 既有索引），才動筆寫計畫；計畫核准後才正式動手寫程式。

練習 4 
順序沒有變化：先讓 agent 只出重構提案（不動檔案）→ 我核對提案的合理性後核准 →
才動手改 `OrderService.cs` → 交叉驗證（code review + 跑測試兩個獨立檢查）→ 我自己再重讀
一次完整 diff 並獨立重跑 build/test 確認一致 → commit（`fd4da51`）。

### 2. AI 幫上大忙的地方

練習 3 
先派兩個 `Explore` agent 平行查既有慣例，這一步很有效。給的其中一個 prompt：

> In the repo at C:\Users\dm23\source\repos\traning\training-repo, read these files in full
> and report their complete relevant content/structure ... 1. src/OrderHub.Web/Controllers/
> ProductsController.cs — full content 2. src/OrderHub.Core/Services/IProductService.cs and
> ProductService.cs — full content ... Report back with actual code excerpts (not
> paraphrased), especially method signatures, DI registration lines, and the exact Razor
> form/pagination pattern...

有效的原因是明講「要完整內容、不要摘要」，逼 agent 回報真正的程式碼片段而不是印象式描述。
這份回報後來讓我在設計 `LowStock` action 時，能直接對照 `OrdersController.Index` 既有的
純量參數寫法，而不是憑印象猜一個新寫法。

練習 4 
讓 agent 只出重構提案、不直接動手這一步也很有效。提案本身被要求先讀
`OrderService.cs`、`IOrderService.cs`、`ServiceResult.cs`、`OrderServiceCreateTests.cs`，
再具體回答「抽出後的簽章長怎樣、放哪個檔案、怎麼確保行為不變、範圍以外不動什麼」，逼出
一份可以直接照做、含程式碼片段的提案，而不是一句「幫我重構」就直接改檔案。

### 3. AI 誤導我的地方，與我如何發現

Repository 查詢的設計理論上可行，實際一跑就炸。
練習 3 的計畫裡，`GetLowStockAsync`
原本設計成「`GroupBy` 聚合近 30 天銷量 → `join ... into ... from ... DefaultIfEmpty()`
左外聯回 Products」一次查完，計畫文件裡也寫了一段「⚠️ SQL 轉譯風險」提醒要對真的
SQL Server 驗證。照這個設計寫完後，跑 `dotnet test --filter
FullyQualifiedName~ProductServiceTests` 直接炸兩個測試：

```
System.InvalidOperationException : Nullable object must have a value.
   at OrderHub.Infrastructure.Repositories.ProductRepository.GetLowStockAsync(Int32 threshold)
失败!  - 失败:     2，通过:     3，已跳过:     0，总计:     5
```

這是 EF Core 對 anonymous type 左外聯 null 判斷的已知陷阱，連在寬鬆的 InMemory provider 上
都會炸，證明「理論上應該可以轉譯」不等於真的可行。是靠直接跑測試抓到的，不是肉眼看
LINQ 語法看出來的。改法是拆成兩次固定查詢（先查 Products、再對這些 id 做一次 `GroupBy`
聚合成 Dictionary，在記憶體組合），改完後對本機真正的 SQL Server 跑 `dotnet run`，用
EF Core 的查詢 log 確認只送出 2 次 SQL，不是 N+1。

過度採信一份審查報告，做了一次錯誤的修正。練習 3 第一版計畫寫完後，讓 agent 另外派
一個審查角色去挑計畫毛病，它指出「這個 repo 沒有任何 GET action 用 DataAnnotations 驗證
過，直接用會是自創寫法」，因此把 `threshold` 驗證改成「純量參數 + 手動
`ModelState.AddModelError`」。後來又重審一次計畫，才真的去讀了一個先前沒讀過的檔案
`CreateOrderLineViewModel.cs`，發現 `Quantity` 欄位本來就是用
`[Range(1, 999, ErrorMessage = "數量需介於 1 到 999")]` 在驗證同一種「單一正整數」需求——
代表前面那份審查報告的結論其實是錯的。是靠直接讀原始檔案而不是相信審查摘要抓到的，
最後改回用 `[Range]`。

同一份審查報告裡還有兩個可查證的錯誤引用。 它說 `PagedResult<T>` 放在 `Core/Domain`
（實際上在 `Core/Common`），也說某個測試寫法（`Items = { new OrderItem {...} } }` 巢狀
collection initializer）出現在 `OrderServiceQueryTests.cs`／`OrderServiceCancelTests.cs`
（實際上這兩個檔案都沒有這段寫法，是在 `OrderServicePricingTests.cs` 裡）。把它點名的
檔案逐一重新打開核對才發現這兩處引用是假的，不是報告說有就真的有。

### 4. 我會帶回日常工作的一招

agent／審查報告點名某個既有慣例或檔案當「先例」時，動手前先自己打開那個檔案核對一次，不要直接採信。

操作步驟：
1. 看到「這個寫法比照 XXX 檔案的慣例」這類句子，先記下被點名的確切檔案路徑
2. 打開那個檔案，找到被引用的那一段程式碼，逐字核對「真的長那樣嗎」
3. 如果對不上（像這次 `PagedResult<T>` 的位置、`Items = {...}` 寫法的出處都對不上），
   當場更正引用來源，不要繼續沿用錯的結論往下寫
4. 對於「理論上應該可以轉譯／應該不會有問題」這種只靠推理、沒有既有先例可抄的新寫法
   （例如這次的 EF Core `GroupBy` + 左外聯），先跑一次測試或對真實環境驗證再收尾，
   不要讓「寫計畫時的推理」取代「實際執行的結果」

練習 3 同一個計畫審查循環裡，一次就抓到三個「被引用的先例其實不存在」的例子，值得養成
每次看到「這裡沿用了 XXX 的慣例」都回頭核對一次的反射動作。


## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1
commit `2e8b85e 練習 1 — 讓 agent 讀懂專案, agent 初始設置`，
內容是 `.claude/settings.json`（`allow`/`ask`/`deny` 權限清單，例如 `Bash(rm -rf *)`／
`git push --force`／`git reset --hard` 都在 `deny`，`dotnet ef database drop`／`git push`
在 `ask`）、兩個 hook（`block-destructive-sql.ps1` 掛在 `PreToolUse`、`log-edits.ps1` 掛在
`PostToolUse` 記錄每次 Edit/Write）、兩個 subagent（`code-reviewer`、`test-runner`）、
一個 `fix-bug` skill：

✅ 三個專案各自的職責：
   - `OrderHub.Web`：Controller、ViewModel、Razor View，只做接線與顯示，不直接碰
     `DbContext`
   - `OrderHub.Core`：domain model（`Order`/`OrderItem`/`Product`/`Customer`）、
     service 介面與商業邏輯（例如折扣率、金額計算、驗證規則），不依賴任何其他專案
   - `OrderHub.Infrastructure`：`OrderHubDbContext`、repository 實作、migrations、
     `DbSeeder`，是唯一允許碰 EF Core 的地方，依賴 `Core`

✅ 核對過建單流程，並找到一處過度簡化的說法：直接讀 `OrderService.cs` 的
   `CreateOrderAsync`（35–79 行）與 `CalculateTotal`（142–147 行），核對一個容易被過度
   簡化的假設：「建單當下把最終應付金額算好存起來就好」。實際上 `Order`／`OrderItem`
   （`Core/Domain`）根本沒有 `Total`／`Subtotal` 欄位，`CreateOrderAsync` 只把原價存進
   `OrderItem.UnitPriceSnapshot`（68 行），應付金額永遠是之後呼叫 `CalculateTotal` 時，
   依 `order.Customer.Tier` 現算（144–146 行）才得出——不是建單當下算好存下來的固定值，
   而且這個計算依賴 `Order.Customer` 有被 include，若呼叫端只查 `Order` 沒帶入
   `Customer` 就直接呼叫 `CalculateTotal`，`tier` 會靜默 fallback 成 `Standard`
   （144 行 `?? CustomerTier.Standard`）算錯折扣，而不是丟例外提醒你資料沒帶全。

✅ 商業邏輯該放哪一層、新增頁面要動哪些地方：依 `CLAUDE.md` 的分層慣例，商業邏輯
   （篩選條件、聚合計算、驗證規則）一律放 `Core` 的 service／repository，不進
   Controller；`Controller` 只做 `ModelState` 檢查＋呼叫 service＋手動 map 成
   `ViewModels/` 底下的 ViewModel。若要新增一個唯讀查詢頁面，預期要動的地方是：
   `Core/Interfaces` 加介面方法（或延伸既有介面）、對應 service 實作商業邏輯、
   `Infrastructure/Repositories` 加查詢方法（唯一允許碰 `OrderHubDbContext` 的地方）、
   Controller 加 action、新增 ViewModel 與 View；如果新頁面用的是既有已註冊類別上新增的
   方法，就不用動 `Program.cs` 的 DI 註冊，只有新增全新類別時才需要註冊。

練習 2
✅ 三個 bug 的根因都是先從症狀出發，由 Controller 往下追到 Service/Repository 才定位到：
   - 分頁：`OrderRepository.GetPagedAsync` 的 `.Skip(page * pageSize)` 少了 `-1`，
     第一頁把最新的 20 筆整批跳過
   - Gold 折扣：`CreateOrderAsync` 對 Gold 會員把折扣先套進 `UnitPriceSnapshot`，
     `CalculateTotal` 又依 tier 打一次折，變成雙重折扣
   - 取消庫存：`CancelOrderAsync` 先把 `order.Status` 改成 `Cancelled`，才用
     `if (order.Status == Pending || Confirmed)` 判斷要不要回補庫存，條件永遠是 false

✅ 每個修復都回到頁面實測，確認症狀消失才進到下一步。
✅ 每個 bug 都補了一個回歸測試，且都先驗證過「修復前會失敗」：
   - `GetOrders_FirstPage_IncludesNewestOrder`：改回舊版 `.Skip(page * pageSize)` 時，
     斷言「第一頁包含最新訂單」失敗；修復後 29 個測試全部通過
   - `CreateOrder_ForGoldCustomer_SnapshotsFullUnitPriceNotDiscounted`：改回舊版時
     `Assert.Equal(1000m, ...)` 實際得到 `900.00`；修復後 30 個測試全部通過
   - `CancelOrder_ActiveOrder_RestoresProductStock`：改回舊版時斷言「庫存應回補到 10」
     實際還是 `7`；修復後 32 個測試全部通過

練習 3
✅ `/Products/LowStock` 不帶參數時門檻顯示 10，能查到 active 且庫存 < 10 的商品，庫存 < 5
   的列有 `table-danger`；帶 `?threshold=3`（或更寬的 `?threshold=100`）結果會隨之改變
   （用 `curl` 對 `dotnet run` 起來的本機站台實測，非只看程式碼）。
✅ `?threshold=0`、`?threshold=-1`、`?threshold=abc` 三種都回 HTTP 200，不是 500，且各自
   顯示驗證錯誤（0/-1 顯示「門檻必須大於 0」；`abc` 顯示 ASP.NET Core 內建的型別轉換錯誤
   訊息），逐一用 `curl` 實測確認。
✅ 售出數量欄位排除了 Cancelled 訂單，且測試刻意在 30 天邊界前後各 1 小時取值
   （`AddDays(-30).AddHours(1)` 應計入、`AddDays(-30).AddHours(-1)` 應排除），而不是只測
   離邊界很遠、比較保險的日期。
✅ 停售商品即使庫存低於門檻也不會出現（`GetLowStock_ExcludesInactiveProducts`）。
✅ 分層與命名跟既有 `Products`／`Orders` 功能一致：`LowStockListViewModel` 比照
   `OrderListViewModel`／`ProductListViewModel` 命名；`Threshold` 驗證比照
   `CreateOrderLineViewModel.Quantity` 用 `[Range]`；除了 agent 自我 review，過程中另外
   派了一個獨立審查角色對照規格與 `CLAUDE.md` 逐條挑錯（見上方第 3 題），挑出了 4 個
   實際問題並修正。
✅ 補了 3 個 service 測試（`GetLowStock_FiltersByThresholdAndSortsByStockAscending`、
   `GetLowStock_ExcludesInactiveProducts`、
   `GetLowStock_QuantitySoldLast30Days_ExcludesCancelledAndOldOrders`），`dotnet test`
   35/35 全綠，`dotnet build` 0 warning、0 error。commit `b5ca6af`。

練習 4
✅ 重構後 `dotnet test` 35/35 全綠（含建單既有 11 個測試與練習 2、3 補的所有回歸測試）。
✅ 這次重構「改善了什麼」：把 `CreateOrderAsync` 裡的驗證判斷抽成兩個
   `private static` 方法（`ValidateOrderHeader`／`ValidateOrderLine`），方法本體只剩流程
   編排。「沒有改變什麼」：`errors.Add`/`continue` 的累積邏輯、庫存扣減、`order.Items.Add`
   仍留在原本的迴圈裡；`IOrderService` 介面、`ServiceResult<T>`、`CancelOrderAsync` 等其他
   方法完全沒動；六則錯誤訊息文字逐字保留，沒有重打。
✅ 自己重讀過一次完整 diff（不是只看 agent 說「沒問題」）：逐行核對 `ValidateOrderHeader`
   仍是「找到第一個錯就 return」而不是變成累積、三處 null-forgiving（`customer!`／`lines!`
   ／`product!`）各自都有前置的驗證檢查保護，並獨立重跑一次 `git status`／`dotnet build`／
   `dotnet test` 確認只有 `OrderService.cs` 一個檔案被改動、結果與另外兩個獨立檢查
   （code review + 跑測試）的回報一致。commit `fd4da51`。

---

## 附錄：值得留下的對話片段

### 對話 1：規格其實沒貼上，先問清楚再動手

我的 prompt：`我要新增「低庫存警示頁面」，規格如下（貼上上面整段規格）。先不要寫程式，
請給我一份實作計畫...`——但「規格如下」後面實際是空的。

agent 回應摘要：沒有照著空白規格硬猜著寫計畫，而是先明講「規格如下後面是空白，要怎麼
補齊」，給出兩個選項（重貼規格／先問幾個關鍵問題），選擇重貼後才拿到完整規格繼續。

值得保留的原因：與其對著缺漏的輸入自己腦補一份計畫，不如先攔下來確認，成本比事後整份
計畫重寫低很多。

### 對話 2：連續要求「再檢查一次」才逼出真正的問題

我的 prompt（依序）：「檢查多一次真的如我的要求的嗎」「再檢查一次」，最後一次改成明確的
五點清單（分層／慣例／邊界／測試對應規格三件事／有沒有夾帶額外重構）。

agent 回應摘要：前兩次都是自己重讀計畫、抓出並修正一些問題（例如把 `LowStockProductInfo`
從錯誤引用的 `Core/Domain` 改回 `Core/Common`）；直到給出具體清單後，才改用「另外派一個
獨立角色去對照規格原文與實際檔案挑錯」的方式，而不是同一個角色再看一次同一份計畫——這一步
真正挑出了三個可查證的錯誤（見上方第 3 題）。

值得保留的原因：同一句「再檢查一次」連問三次沒有用，換一個要求更具體、逼對方換方法的問法，
才真的挑出問題——這比籠統地說「再檢查一次」更有效。
