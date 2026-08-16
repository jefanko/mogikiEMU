# Mogiki ファミコン/NES エミュレータ (mgkEMU)

<div align="center">
  <img src="mgkEMU.png" alt="Mogiki NES Emulator Icon" width="128" height="128" />
  <h3>モダンな Avalonia UI を搭載した、サイクル精度の高いファミリーコンピュータ / NES エミュレータ。</h3>

  <p>
    <a href="README.md">English</a> |
    <b>日本語</b> |
    <a href="README.zh-TW.md">繁體中文 (台灣)</a>
  </p>
</div>

---

## 🌟 概要

**Mogiki**（模擬器 - もぎき、シミュレータ / エミュレータの意）は、**C# (.NET 10)** で再設計され、モダンでハードウェアアクセラレーションに対応した **Avalonia UI 11.x** を備えるサイクル精度のファミコン / NES エミュレータです。

本プロジェクトは**「動作だけでなくハードウェアそのものを再現する」**という設計思想に基づき、NES 内部バスと各コンポーネントの相互作用をサイクル単位でシミュレートすることで、本物の実機挙動、正確な音声合成、ラスター分割タイミングを忠実に再現します。

---

## ✨ 主な機能

### 🖥️ モダンな Avalonia UI
- **ダーク Fluent テーマ**: `Avalonia.Themes.Fluent` と Inter フォントによる洗練されたモダン UI とカスタムステータスバッジ。
- **ハードウェアアクセラレーション描画**: `WriteableBitmap` へのダイレクトメモリアクセスにより、低負荷で極めて安定した 60.1 FPS 描画を実現。
- **ドラッグ＆ドロップ**: Windows Explorer から `.nes` ROM ファイルをウィンドウにドロップするだけで即座にプレイ可能。
- **最近プレイしたゲーム履歴**: 直近 10 件の ROM を自動で記憶し、メニューからすばやく起動。
- **画面アスペクト比の切り替え**:
  - `4:3 Standard TV`（標準テレビ比率）
  - `8:7 Authentic NTSC PAR`（実機準拠ピクセル比率）
  - `1:1 Pixel Perfect`（正方形ピクセル）
- **グラフィックフィルター**: ニアレストネイバー（くっきりしたドット絵）またはバイリニア補間（滑らかな描画）。
- **スクリーンショット撮影**: `F12` キーで現在の画面を PNG 画像として保存。
- **インタラクティブな設定ダイアログ**:
  - **コントローラー設定**: ボタンをクリックしてキーを押すだけで直感的にキーバインドを変更。
  - **パターンテーブルビューア**: リアルタイムで CHR ROM タイルバンクとパレット配色を確認。
  - **About ダイアログ**: エミュレータのアーキテクチャとハードウェア仕様を確認。

### 🎮 エミュレーションコア (`Mogiki.Core`)
- **Ricoh 2A03 CPU**: 未定義・非公式命令に対応した完全な 6502 コア。アンマネージド関数ポインタによる高速な命令ディスパッチ。
- **Ricoh 2C02 PPU**: サイクル精度の描画パイプライン、8x8 / 8x16 スプライト、スプライト評価、PPUMASK の左端クリッピング、奇数フレームのサイクルスキップを忠実に再現。
- **APU 2A03**: 2つのパルス波、三角波、ノイズ波（LFSR）、DMC サンプルを含む完全な音声合成と低遅延リングバッファ。
- **対応マッパー (Mapper)**:
  - **Mapper 0 (NROM)**: 『スーパーマリオブラザーズ』『ドンキーコング』『パックマン』
  - **Mapper 1 (MMC1)**: 『ゼルダの伝説』『メトロイド』『ロックマン2』
  - **Mapper 2 (UxROM)**: 『悪魔城ドラキュラ』『ロックマン』『ダックテイルズ』
  - **Mapper 4 (MMC3)**: 『スーパーマリオブラザーズ3』『星のカービィ 夢の泉の物語』（スキャンラインリロード状態遷移と正確な IRQ タイミング）
  - **Mapper 5 (MMC5)**: 『悪魔城伝説』（デュアルバンク 8x16 CHR、1KB スキャンライン IRQ、ハードウェア乗算器）
  - **Mapper 69 (Sunsoft FME-7)**: 『ギミック!』『バットマン リターン・オブ・ザ・ジョーカー』

---

## 🕹️ デフォルトキーボード操作

| ファミコンボタン | キーボードキー | 説明 |
|:---|:---|:---|
| **十字キー 上** | `↑ (上矢印)` | 上移動 / ハシゴを登る |
| **十字キー 下** | `↓ (下矢印)` | 下移動 / しゃがむ |
| **十字キー 左** | `← (左矢印)` | 左移動 |
| **十字キー 右** | `→ (右矢印)` | 右移動 |
| **A ボタン** | `X` | ジャンプ / アクション |
| **B ボタン** | `Z` | 攻撃 / ダッシュ |
| **START** | `S` | スタート / ポーズメニュー |
| **SELECT** | `A` | セレクト / アイテム選択 |
| **ROM を開く** | `Ctrl + O` / `F1` | ファイル選択ダイアログを表示 |
| **一時停止 / 再開** | `Space` / `P` | エミュレーションの一時停止 |
| **リセット** | `Ctrl + R` | 本体のリセット |
| **高速送り (Turbo)** | `Tab` | ターボモード（倍速） |
| **スクリーンショット** | `F12` | PNG 画像として保存 |

> *※ すべてのキーバインドは **Config $\rightarrow$ Controller Configuration...** より自由に変更可能です。*

---

## 🚀 ビルドと実行方法

### 必要な環境
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### .NET CLI で実行する
```powershell
# エミュレータを起動
dotnet run --project src/Mogiki.App

# または直接 ROM を指定して起動
dotnet run --project src/Mogiki.App "path/to/game.nes"
```

### スタンドアロン実行ファイルをビルド・発行する
```powershell
dotnet publish src/Mogiki.App/Mogiki.App.csproj -c Release -o ./dist
```
ビルド完了後、`dist/Mogiki.App.exe` が生成されます。

### 自動単体テストを実行する
```powershell
dotnet test
```

---

## 🏛️ プロジェクト構成

```
mogikiEMU/
├── src/
│   ├── Mogiki.Core/          # クロスプラットフォーム対応エミュレーションコア (.NET 10)
│   │   ├── Cpu/              # Ricoh 2A03 6502 CPU
│   │   ├── Ppu/              # Ricoh 2C02 PPU & Loopy レジスタ
│   │   ├── Apu/              # 2A03 APU 音声合成
│   │   ├── Bus/              # メインバス相互接続 & DMA
│   │   ├── Cartridge/        # iNES ROM ローダー
│   │   └── Mappers/          # マッパー 0, 1, 2, 4, 5, 69
│   └── Mogiki.App/           # モダンな Avalonia UI フロントエンド
│       ├── Views/            # MainWindow, ControllerConfig, PatternTable, About
│       ├── Audio/            # ローパスフィルター付き AudioEngine
│       ├── Config/           # AppConfig & キー設定管理
│       └── Assets/           # アプリアイコン & ブランディング素材
├── tests/
│   └── Mogiki.Tests/         # xUnit 自動テストスイート
├── srcLegacy/                # オリジナル C++ リファレンス実装
└── build.bat                 # レガシー C++ ビルドスクリプト (mgkEMU_legacy.exe)
```

---

## 📜 ライセンス
本プロジェクトは MIT ライセンスの下でオープンソースとして公開されています。
