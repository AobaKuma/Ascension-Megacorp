# Ascension Megacorp (USAC)

**作者 / Author:** AOBA  
**Package ID:** `AOBA.USAC.Core`  
**支援版本 / Supported Version:** RimWorld 1.6

---

## 項目簡介 / Overview

本 Mod 為 RimWorld 的軍事/機兵擴充模組，圍繞「聯合星際武裝公司（United Stellar Armament Company，USAC）」這一派系展開，為遊戲增加機兵部隊、特殊武器系統、交易機制及採礦突襲事件。

This mod is a military/mech expansion for RimWorld centered around the faction **United Stellar Armament Company (USAC)**, adding mech units, heavy weapon systems, custom trade mechanics, and mining raid events.

---

## 項目架構 / Project Architecture

```
Ascension-Megacorp/
├── About/                        # Mod 元資料 (名稱、作者、依賴)
├── 1.6/                          # RimWorld 1.6 主要 Mod 內容
│   ├── Assemblies/               # 編譯後 DLL (USAC.dll)
│   ├── AssetBundles/             # Unity 資源包
│   ├── CE/                       # Combat Extended 相容補丁
│   ├── Defs/                     # XML 定義檔
│   │   ├── Abilities/            # 能力定義 (武器能力、Omaha 能力)
│   │   ├── Backstories/          # 背景故事
│   │   ├── Effects/              # 視覺特效
│   │   ├── FactionDef/           # USAC 派系定義
│   │   ├── IncidentDef/          # 事件 (商船到訪、採礦突襲)
│   │   ├── JobDef/               # 工作定義
│   │   ├── Misc/                 # 其他雜項定義
│   │   ├── NeedDef/              # 需求定義 (機兵備戰度)
│   │   ├── Overlay/              # 覆蓋層
│   │   ├── PawnkindDef/          # 兵種定義
│   │   ├── RulePackDef/          # 命名規則
│   │   ├── ThingDef_Building/    # 建築 (屍體袋、貨箱、採礦鑽機、機兵殘骸)
│   │   ├── ThingDef_Misc/        # 道具 (外骨骼、頭盔、武器、消耗品、債券、訂單)
│   │   ├── ThingDef_Race/        # 機兵種族 (Cobalt、Rocky、Paraman、HeavyMisc)
│   │   ├── TraderKindDef/        # 商人種類
│   │   └── WorkGiverDef/         # 工作給予定義
│   ├── Ideology/                 # Ideology DLC 相容內容
│   ├── Patches/                  # XML 補丁
│   └── Realistic_Body/           # 寫實體型補丁
├── Languages/                    # 本地化
│   ├── English/
│   ├── ChineseSimplified (简体中文)/
│   └── ChineseTraditional (繁體中文)/
├── Sounds/                       # 音效
├── Textures/                     # 貼圖素材
│   ├── Effects/
│   ├── Icons/
│   ├── Things/
│   ├── UI/
│   └── World/
├── UnityProject/                 # Unity 編輯器專案 (著色器、資源包)
├── _Sources/USAC/                # C# 原始碼
│   ├── Ability/                  # 能力邏輯
│   ├── Core/                     # 核心系統
│   ├── CorpseBag/                # 屍體袋機制
│   ├── DefOf/                    # 定義常數
│   ├── Effects/                  # 特效
│   ├── Items/                    # 道具效果
│   ├── Mech/                     # 機兵邏輯
│   ├── MiningRaid/               # 採礦突襲系統
│   ├── Patch/                    # Harmony 補丁
│   ├── Trade/                    # 交易系統
│   └── USAC.csproj               # .NET 4.72 專案檔
├── LoadFolders.xml               # RimWorld 載入設定
└── TODO                          # 開發待辦清單
```

---

## C# 模組架構 / C# Module Architecture

| 模組 | 檔案 | 功能說明 |
|------|------|----------|
| **Core** | `HarmonyEntry.cs` | Harmony 入口點，初始化所有補丁 |
| | `USAC_AssetBundleLoader.cs` | 載入 Unity 資源包 |
| | `USAC_Cache.cs` | 遊戲資料快取 |
| | `GameComponent_USACTrader.cs` | 管理 USAC 商人狀態 |
| | `GameComponent_USACHostilityReset.cs` | 敵意重置邏輯 |
| | `MapComponent_VisualPawnMounts.cs` | 地圖上的視覺化乘騎系統 |
| | `CompVisualPawnContainer.cs` | 視覺化兵員容器元件 |
| **Mech** | `CompMechReadiness.cs` | 機兵備戰度需求元件 |
| | `CompBulletDeflect.cs` | 子彈偏轉元件 |
| | `CompMechWreck.cs` | 機兵殘骸元件 |
| | `Need_Readiness.cs` | 備戰度 Need |
| | `Skyfaller_MechIncoming.cs` | 機兵空降特效 |
| | `USACMechStatInitializer.cs` | 機兵數值初始化 |
| **Trade** | `IncidentWorker_USACTraderArrival.cs` | 商人到訪事件處理器 |
| | `StockGenerator_USAC_Mechs.cs` | 機兵商品生成 |
| | `StockGenerator_USACBond.cs` | 債券商品生成 |
| | `StockGenerator_BuyCorpseBag.cs` | 收購屍體袋 |
| | `Tradeable_USACCurrency.cs` / `Tradeable_Bond.cs` / `Tradeable_CorpseBag.cs` | 自訂交易貨幣與商品 |
| | `USAC_MechTradeUtility.cs` | 交易工具函式 |
| | `ModExtension_MechOrder.cs` | 機兵訂單 Mod 擴充 |
| **Ability** | `CompAbilityEffect_MICLIC.cs` | MICLIC 拖曳式炸藥能力 |
| | `CompAbilityEffect_MineclearingShovel.cs` | 清雷鏟能力 |
| | `Projectile_MICLIC_Towed.cs` | MICLIC 拖曳投射物 |
| | `VerletRope.cs` | Verlet 積分繩索物理 |
| | `Verb_CastAbilityMineclearingShovel.cs` | 清雷鏟施法動詞 |
| | `JobDriver_WaitDetonate.cs` | 等待引爆工作驅動 |
| **CorpseBag** | `Building_CorpseBag.cs` | 屍體袋建築邏輯 |
| | `JobDriver_PackCorpse.cs` | 打包屍體工作驅動 |
| | `ModExtension_CorpseBag.cs` | 屍體袋 Mod 擴充 |
| **MiningRaid** | `IncidentWorker_USACMiningRaid.cs` | 採礦突襲事件處理器 |
| | `Building_HeavyMiningRig.cs` | 重型採礦鑽機建築 |
| | `Building_Crate.cs` | 貨箱建築 |
| | `LordJob_MiningGuard.cs` | 護礦 AI 任務 |
| | `LordToil_DefendMiningRig.cs` / `LordToil_KillThreats.cs` / `LordToil_BoardMiningRig.cs` | 護礦 AI 行為 |
| | `Skyfaller_MiningRig.cs` / `Skyfaller_CrateIncoming.cs` | 空降特效 |
| | `CrateExtension.cs` | 貨箱 Mod 擴充 |
| **Patch** | `Patch_CorpseBagTrade.cs` | 修補屍體袋交易邏輯 |
| | `Patch_USACGoodwill.cs` | 修補 USAC 好感度邏輯 |
| | `Patch_MiningRaidFaction.cs` | 修補採礦突襲派系邏輯 |

---

## 依賴關係 / Dependencies

| 依賴 | 說明 |
|------|------|
| [Harmony](https://github.com/pardeike/HarmonyRimWorld) (`brrainz.harmony`) | 遊戲方法補丁框架 |
| Fortified Features Framework (`AOBA.Framework`) | 作者自製共用框架（需獨立安裝，不含於本倉庫）|
| RimWorld + Royalty / Ideology / Biotech / Anomaly DLC | 遊戲本體及 DLC |

---

## 完成進度 / Completion Progress

### ✅ 已完成 / Completed

- **核心框架** — Harmony 入口、資源包載入、快取系統
- **機兵系統** — 備戰度 Need、子彈偏轉、殘骸元件、空降特效、數值初始化
- **交易系統** — 商人到訪事件、自訂貨幣（債券）、屍體袋收購、機兵訂單
- **採礦突襲系統** — 突襲事件、重型鑽機、護衛 AI（LordJob/LordToil）、貨箱空降
- **屍體袋系統** — 袋裝建築、打包工作驅動
- **能力系統** — MICLIC 炸藥、清雷鏟、繩索物理
- **XML 定義** — 派系、事件、兵種（Cobalt / Rocky / Paraman / HeavyMisc 機兵）、武器（輕 / 中 / 重）、消耗品、外骨骼裝備
- **插板外骨骼（Modular Exosuit）框架**
- **本地化** — 英文、简体中文、繁體中文

### 🔄 進行中 / In Progress

#### 貼圖 / Textures
- [ ] 屍體袋貼圖
- [ ] 技能圖標

#### XML 定義 / XML Definitions
- [ ] 重型機兵武器（Heavy Mech Weapons）
- [ ] Pawnkind 變體（PawnKind Variants）

#### 其他 / Miscellaneous
- [ ] 各種音效（Sound Effects）

---

## 建構方式 / Build

使用 .NET 4.72，以 Visual Studio 或 `dotnet build` 編譯：

```bash
cd _Sources/USAC
dotnet build USAC.csproj -c Release
```

輸出 DLL 會自動複製至 `1.6/Assemblies/USAC.dll`。

---

## 版本紀錄 / Changelog

詳見 Git 提交記錄。
