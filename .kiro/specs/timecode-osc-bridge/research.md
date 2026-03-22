# Research & Design Decisions

---
**Purpose**: TimecodeBridge のディスカバリーフェーズで得られた調査結果と設計判断の記録
---

## Summary
- **Feature**: `timecode-osc-bridge`
- **Discovery Scope**: New Feature（グリーンフィールド）
- **Key Findings**:
  - OSCライブラリは OscCore（BuildSoft.OscCore）が最高パフォーマンスで、VRChat でも使用実績あり
  - LTC デコードは libltc + LTCSharp（.NETラッパー）が唯一の実用的選択肢
  - MTC 受信は NAudio の MIDI 入力機能 + カスタム Quarter Frame パーサー、または DryWetMidi の MTC 受信機能で実現可能

## Research Log

### OSC ライブラリ選定
- **Context**: 複数ホストへの高頻度 OSC 送信に対応するライブラリの選定
- **Sources Consulted**: NuGet（BuildSoft.OscCore, CoreOSC, SharpOSC）、各 GitHub リポジトリ
- **Findings**:
  - **BuildSoft.OscCore**: .NET Standard 2.1 対応、最高パフォーマンス、GC アロケーション最小。VRChat での実績あり
  - **CoreOSC**: .NET Standard 2.0、シンプルな API、メンテナンスは低頻度
  - **SharpOSC**: .NET 3.5 ターゲット、古い
- **Implications**: BuildSoft.OscCore を採用。高頻度送信（フレーム毎リレー）に最適

### LTC デコード手法
- **Context**: オーディオ入力から LTC を解析する方法
- **Sources Consulted**: libltc（x42/libltc）、LTCSharp（elliotwoods/LTCSharp）、libltc-win
- **Findings**:
  - libltc は C ライブラリで LTC エンコード/デコードの標準実装
  - LTCSharp は libltc の .NET ラッパー（MIT ライセンス）
  - libltc-win は Windows/MSVC 向けビルド
  - LTC は 24, 25, 30 fps のみ対応（29.97 ドロップフレームは 30fps ベース）
- **Implications**: NAudio でオーディオキャプチャ → libltc（ネイティブ DLL）で LTC デコード。LTCSharp が古い場合は P/Invoke で直接呼び出しも検討

### MTC（MIDI Timecode）受信
- **Context**: MIDI 入力から MTC を受信・解析する方法
- **Sources Consulted**: NAudio GitHub、DryWetMidi GitHub Issue #115、MIDI Time Code 仕様
- **Findings**:
  - NAudio: MidiIn クラスで MIDI イベント受信可能だが、MTC 専用 API はない。Quarter Frame メッセージを手動パースする必要あり
  - DryWetMidi: `MidiTimeCodeReceived` イベントで MTC をネイティブサポート
  - MTC は Quarter Frame（8メッセージで1フレーム完成）と Full Frame SysEx の2方式
- **Implications**: NAudio をオーディオ入力（LTC 用）に使用しつつ、MIDI 入力も NAudio の MidiIn を利用。MTC Quarter Frame パーサーは自前実装（仕様が単純なため）

### UI フレームワーク選定
- **Context**: Windows 向けダークテーマ対応のリアルタイム表示アプリ
- **Sources Consulted**: WPF/WinUI 3/Avalonia 比較記事多数
- **Findings**:
  - WPF: 安定性最高、MVVM 成熟、.NET 8 サポート、Windows 専用。ダークテーマはカスタムまたは MaterialDesign
  - WinUI 3: モダン UX（Mica/Acrylic）、Fluent Design、ただしバグ報告多め
  - Avalonia: クロスプラットフォーム、Skia レンダリング、組み込みダークテーマ
- **Implications**: WPF + .NET 8 を採用。Windows 専用要件に合致し、リアルタイム更新の安定性を重視。ダークテーマは MaterialDesignInXaml または自前テーマリソース

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| MVVM + Service Layer | WPF 標準の MVVM にドメインサービス層を追加 | WPF との親和性最高、テスト容易、関心の分離が明確 | サービス間の依存管理が必要 | CommunityToolkit.Mvvm で実装コスト削減 |
| Clean Architecture | Ports & Adapters でハードウェア抽象化 | テスタビリティ最高、ハードウェア差し替え容易 | 構造が複雑、小規模プロジェクトにはオーバー | |
| Event-Driven | イベントバスで疎結合通信 | リアルタイム性向上、コンポーネント独立 | デバッグ困難、イベント追跡が煩雑 | 部分的に採用（Timecode イベント通知） |

## Design Decisions

### Decision: UI フレームワーク → WPF + .NET 8
- **Context**: Windows 専用デスクトップアプリでリアルタイム Timecode 表示とダークテーマが必要
- **Alternatives Considered**:
  1. WinUI 3 — モダン UI だが安定性にやや課題
  2. Avalonia — クロスプラットフォームだが Windows 専用なので利点が薄い
- **Selected Approach**: WPF + .NET 8（LTS）
- **Rationale**: Windows 専用要件、MVVM 成熟度、安定性、リアルタイム UI 更新の実績
- **Trade-offs**: モダン UI エフェクト（Mica 等）は手動実装が必要だが、ライブ現場向けにはシンプルなダークテーマで十分
- **Follow-up**: ダークテーマの実装方式（MaterialDesign vs カスタム ResourceDictionary）を実装時に確定

### Decision: OSC ライブラリ → BuildSoft.OscCore
- **Context**: フレーム毎の高頻度送信と複数ホスト同時送信が必要
- **Alternatives Considered**:
  1. CoreOSC — シンプルだがパフォーマンス最適化なし
  2. SharpOSC — .NET 3.5 ターゲットで古い
- **Selected Approach**: BuildSoft.OscCore
- **Rationale**: GC アロケーション最小、VRChat 実績、.NET Standard 2.1 対応
- **Trade-offs**: API がやや低レベル
- **Follow-up**: バンドル送信サポートの有無を実装時に確認

### Decision: アーキテクチャ → MVVM + Service Layer（部分的イベント駆動）
- **Context**: リアルタイム Timecode 更新とキュートリガーを効率的に処理する必要
- **Alternatives Considered**:
  1. Clean Architecture — このプロジェクト規模にはオーバーエンジニアリング
  2. 純粋 Event-Driven — デバッグ困難
- **Selected Approach**: MVVM + Service Layer。Timecode 更新は C# イベント/IObservable で通知
- **Rationale**: WPF MVVM との自然な統合、適度な疎結合、テスタビリティ確保
- **Trade-offs**: サービス間の直接参照が残るが、DI コンテナで管理
- **Follow-up**: DI コンテナの選定（Microsoft.Extensions.DependencyInjection）

## Risks & Mitigations
- **libltc ネイティブ DLL の配布** — アプリにネイティブ DLL を同梱。x64 ビルドのみサポートでシンプル化
- **MTC Quarter Frame の遅延** — 8メッセージで1フレーム完成のため最大1フレーム遅延。リアルタイム表示には予測補間を検討
- **高頻度 OSC 送信時のネットワーク負荷** — 送信間隔設定で制御。UDP のため送達保証なし（仕様上許容）
- **オーディオデバイスの排他制御** — NAudio の WasapiCapture（共有モード）を使用し、他アプリとの共存を確保

## References
- [BuildSoft.OscCore (NuGet)](https://www.nuget.org/packages/BuildSoft.OscCore) — 高性能 OSC ライブラリ
- [libltc](https://github.com/x42/libltc) — LTC デコード/エンコードの C ライブラリ
- [LTCSharp](https://github.com/elliotwoods/LTCSharp) — libltc の .NET ラッパー
- [NAudio](https://github.com/naudio/NAudio) — .NET オーディオ/MIDI ライブラリ
- [DryWetMidi](https://github.com/melanchall/drywetmidi) — .NET MIDI ライブラリ（MTC サポート）
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/) — MVVM インフラストラクチャ
