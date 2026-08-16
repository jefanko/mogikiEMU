# Mogiki 紅白機/NES 模擬器 (mgkEMU)

<div align="center">
  <img src="mgkEMU.png" alt="Mogiki NES Emulator Icon" width="128" height="128" />
  <h3>搭載現代化 Avalonia UI 的週期精確任天堂紅白機 (Famicom / NES) 模擬器。</h3>

  <p>
    <a href="README.md">English</a> |
    <a href="README.ja.md">日本語</a> |
    <b>繁體中文 (台灣)</b>
  </p>
</div>

---

## 🌟 專案概述

**Mogiki**（源自日文「模擬器」之意）是一款採用 **C# (.NET 10)** 重構、具備週期級硬體精確度，並結合現代化 **Avalonia UI 11.x** 硬體加速介面的紅白機 (NES / Famicom) 模擬器。

核心遵循**「模擬硬體本質而非單純重現行為」**的哲學，逐週期模擬 NES 內部匯流排與各元件的互動，忠實還原最純粹的原機體驗、精準的音訊合成與光柵分割掃描線時序。

---

## ✨ 核心特色

### 🖥️ 現代化 Avalonia UI
- **深色 Fluent 主題**：採用 `Avalonia.Themes.Fluent` 與 Inter 字型，呈現精緻俐落的現代化外觀與自訂狀態膠囊。
- **GPU 硬體加速畫面**：透過 `WriteableBitmap` 直接記憶體映射繪圖，實現極致穩定且低延遲的 60.1 FPS 畫面。
- **拖放 (Drag & Drop) 即玩**：從 Windows 檔案總管將 `.nes` ROM 檔案直接拖入視窗即可立刻開始遊玩。
- **最近遊玩清單**：自動記錄並儲存最近遊玩的 10 款遊戲，方便隨時快速載入。
- **畫面比例自由切換**：
  - `4:3 Standard TV`（經典標準電視比例）
  - `8:7 Authentic NTSC PAR`（原機像素長寬比）
  - `1:1 Pixel Perfect`（正方形像素完美呈現）
- **視訊濾鏡選擇**：最近鄰點取樣（極致清晰像素顆粒）或雙線性插值（平滑圓潤畫面）。
- **內建螢幕截圖**：按下 `F12` 鍵即可將當前遊戲畫面儲存為無損 PNG 圖片。
- **互動式工具與設定視窗**：
  - **控制器設定視窗**：點擊目標按鈕並按下鍵盤按鍵即可直覺自訂鍵位，並支援一鍵重設預設值。
  - **圖樣表 (CHR Pattern Table) 檢視器**：即時瀏覽 CHR ROM 圖樣區塊與調色盤色彩配置。
  - **關於視窗**：查看模擬器核心架構與各項硬體規格支援說明。

### 🎮 模擬硬體核心 (`Mogiki.Core`)
- **Ricoh 2A03 CPU**：完整實作 6502 指令集（含非官方未定義指令支援），並透過非受控函數指標 (Function Pointer) 實現零開銷高速指令分派。
- **Ricoh 2C02 PPU**：週期精確的像素繪圖管線，完整支援 8x8 / 8x16 精靈、精靈評估、PPUMASK 左側邊界裁切與奇數幀週期跳步。
- **APU 2A03**：完整音訊合成系統，包含 2 組脈衝波 (Pulse)、1 組三角波 (Triangle)、1 組雜訊波 (Noise/LFSR)、1 組 DMC 取樣通道，搭配低延遲無鎖環形緩衝區。
- **支援卡帶晶片 (Mapper)**：
  - **Mapper 0 (NROM)**：《超級瑪利歐兄弟》、《大金剛》、《小精靈》
  - **Mapper 1 (MMC1)**：《薩爾達傳說》、《銀河戰士》、《洛克人2》
  - **Mapper 2 (UxROM)**：《惡魔城》、《洛克人》、《唐老鴨俱樂部》
  - **Mapper 4 (MMC3)**：《超級瑪利歐兄弟3》、《星之卡比 夢之泉物語》（精確掃描線計數重載狀態機與 IRQ 分割時序）
  - **Mapper 5 (MMC5)**：《惡魔城傳說》（雙區塊 8x16 CHR、1KB 掃描線 IRQ、硬體乘法器）
  - **Mapper 69 (Sunsoft FME-7)**：《Gimmick!》、《蝙蝠俠：小丑歸來》

---

## 🕹️ 預設鍵盤操作對照表

| NES 原生按鍵 | 預設鍵盤按鍵 | 功能說明 |
|:---|:---|:---|
| **方向鍵 上** | `↑ (上方向鍵)` | 向上移動 / 攀爬 |
| **方向鍵 下** | `↓ (下方向鍵)` | 向下移動 / 蹲下 |
| **方向鍵 左** | `← (左方向鍵)` | 向左移動 |
| **方向鍵 右** | `→ (右方向鍵)` | 向右移動 |
| **A 鍵** | `X` | 跳躍 / 確認動作 |
| **B 鍵** | `Z` | 攻擊 / 衝刺加速 |
| **START** | `S` | 開始遊戲 / 選單暫停 |
| **SELECT** | `A` | 選擇 / 切換道具 |
| **開啟 ROM 檔案** | `Ctrl + O` / `F1` | 開啟檔案選取對話框 |
| **暫停 / 繼續** | `Space` / `P` | 暫停遊戲模擬 |
| **主機重置** | `Ctrl + R` | 重置主機系統 |
| **加速模式 (Turbo)** | `Tab` | 遊戲快速前進 |
| **螢幕截圖** | `F12` | 擷取並儲存 PNG 截圖 |

> *※ 所有鍵位均可在 **Config $\rightarrow$ Controller Configuration...** 選單中自訂。*

---

## 🚀 建置與執行說明

### 事前準備
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 使用 .NET CLI 直接執行
```powershell
# 啟動模擬器
dotnet run --project src/Mogiki.App

# 或在啟動時直接指定載入 ROM
dotnet run --project src/Mogiki.App "path/to/game.nes"
```

### 建置並發行獨立執行檔
```powershell
dotnet publish src/Mogiki.App/Mogiki.App.csproj -c Release -o ./dist
```
發行完成後，可在 `dist/Mogiki.App.exe` 找到編譯好的執行檔。

### 執行自動化單元測試
```powershell
dotnet test
```

---

## 🏛️ 專案架構目錄

```
mogikiEMU/
├── src/
│   ├── Mogiki.Core/          # 跨平台核心模擬程式庫 (.NET 10)
│   │   ├── Cpu/              # Ricoh 2A03 6502 CPU 核心
│   │   ├── Ppu/              # Ricoh 2C02 PPU 與 Loopy 暫存器
│   │   ├── Apu/              # 2A03 APU 音訊合成器
│   │   ├── Bus/              # 主系統匯流排與 DMA 傳輸
│   │   ├── Cartridge/        # iNES 卡帶載入解析
│   │   └── Mappers/          # 卡帶晶片 (Mapper 0, 1, 2, 4, 5, 69)
│   └── Mogiki.App/           # 現代化 Avalonia UI 前端應用程式
│       ├── Views/            # MainWindow, ControllerConfig, PatternTable, About
│       ├── Audio/            # 低通濾波音訊引擎 (AudioEngine)
│       ├── Config/           # AppConfig 與按鍵設定管理
│       └── Assets/           # 應用程式圖示與品牌資產
├── tests/
│   └── Mogiki.Tests/         # xUnit 自動化測試套件
├── srcLegacy/                # 原始 C++ 對照基準實作
└── build.bat                 # 舊版 C++ 建置腳本 (mgkEMU_legacy.exe)
```

---

## 📜 授權條款
本專案採用 MIT 授權條款開放原始碼。
