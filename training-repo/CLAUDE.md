# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 專案簡介

OrderHub 是公司內部訂單管理系統：業務可建立/查詢訂單、管理商品與客戶。內部使用、單一 SQL Server
資料庫，規模小（20 客戶、50 商品），不需要考慮多租戶、微服務或高併發架構。

本專案同時也是內部 AI Agent 實作培訓的練習素材（訓練活動說明在上一層目錄
`../documents/`，例如 `../documents/PROCESS.md`、`../documents/activities/activity-guideline.md`）。
與此相關的一個原則：修 bug 時先依使用者實際描述的症狀去頁面重現、往下定位根因，不要在使用者
還沒要求前就主動列出/修改其他看起來可疑的邏輯。

## 技術棧

- .NET 8 / ASP.NET Core MVC（Razor Views + Bootstrap 5，前端資源皆為本地檔案，不依賴 CDN）
- EF Core 8（`Microsoft.EntityFrameworkCore.SqlServer`）+ SQL Server（本機安裝，不使用 Docker）
- 測試：xUnit + EF Core InMemory provider（`tests/OrderHub.Tests`，**不需要**、也不會動到本機 SQL Server）
- 沒有登入/授權機制（`Program.cs` 未啟用 `UseAuthentication`），也沒有 AutoMapper，所有 mapping 手寫

## 專案結構與分層

```
src/
├── OrderHub.Web/            # Controllers、ViewModels、Views（只做接線與顯示）
├── OrderHub.Core/           # Domain models、service 介面與商業邏輯（無其他專案依賴）
└── OrderHub.Infrastructure/ # EF Core DbContext、repositories、migrations、DbSeeder（依賴 Core）
tests/
└── OrderHub.Tests/          # xUnit（InMemory DB），一個 test class 對應一個 service 的一組行為
```

分層慣例（新增功能時請遵循）：

- Controller 保持薄，只轉接 service 結果並手動 map 成 ViewModel；商業邏輯一律放 Core 的 service
- 只有 `Infrastructure/Repositories/*` 碰 `OrderHubDbContext`；Controller / Service 不可直接用 EF Core
- `OrderService` 的 mutating 方法（`CreateOrderAsync`、`CancelOrderAsync`）回傳 `ServiceResult<T>`
  （`Core/Common/ServiceResult.cs`）表達預期內的業務失敗（`Fail(...)`），不要用例外表達可預期的失敗；
  查詢方法（`GetOrdersAsync` 等）與 `CustomerService`/`ProductService` 目前都直接回傳 domain 型別，
  沒有走 `ServiceResult<T>`
- View 一律綁 `ViewModels/` 底下的 ViewModel，不直接綁 domain model；mapping 手寫在 Controller
  （例如 `OrdersController` 的 `.Select(o => new OrderRowViewModel {...})`），不使用 AutoMapper
- 使用者輸入用 DataAnnotations（`[Required]`/`[Range]`/`[MinLength]` 等）+ `ModelState.IsValid` 驗證；
  service 回傳的業務錯誤也透過 `ModelState.AddModelError(string.Empty, error)` 併入同一個
  `asp-validation-summary` 顯示，兩者都不可讓頁面變成 500
- 操作結果訊息用 `TempData["Success"]` / `TempData["Error"]`，在 `Views/Shared/_Layout.cshtml`
  裡直接內嵌渲染成 Bootstrap alert（沒有另外拆成獨立 partial 檔）
- 金額一律用 `decimal`（EF 設定 `decimal(18,2)`）；折扣率定義在
  `OrderService.GetDiscountRate(CustomerTier)`，訂單金額計算集中在
  `OrderService.CalculateSubtotal` / `CalculateTotal`，不要在別處重算
- 參考檔：新增 Controller 照 `ProductsController.cs`、新增 service 照 `ProductService.cs` 的寫法

## 常用指令

- `dotnet build`：建置整個 solution
- `dotnet test`：跑全部測試（InMemory DB，不需要 SQL Server）
- `dotnet test --filter FullyQualifiedName~OrderServiceCreateTests`：只跑單一 test class
- `dotnet run --project src/OrderHub.Web`：啟動網站，預設 `http://localhost:5150`
  （第一次啟動會自動 `db.Database.Migrate()` + `DbSeeder.SeedAsync`，seed 資料使用固定 random
  seed，多人執行結果一致）
- 重置資料庫：`dotnet ef database drop -f -p src/OrderHub.Infrastructure -s src/OrderHub.Web`，
  再 `dotnet run --project src/OrderHub.Web` 觸發重新 migrate + seed

## 重要 / 危險檔案

- `src/OrderHub.Infrastructure/Migrations/**`：EF migration 是歷史紀錄，不要手改；異動 schema 用
  `dotnet ef migrations add`
- `src/OrderHub.Web/appsettings*.json`：連線字串設定，改動前先問
- `src/OrderHub.Infrastructure/Data/DbSeeder.cs`：改動會影響所有人的種子資料一致性，異動前先問

## 不要做的事

- 不要未經同意就加新的 NuGet 套件
- 不要在 Controller / Service 直接使用 `DbContext`
- 不要為了「順手」重構與當前任務無關的程式碼
- 不要讀取或寫入任何機密檔（`*.pfx`、`appsettings.Production.json`、user-secrets）
