# Research & Design Decisions

## Summary
- **Feature**: `timecode-generation`
- **Discovery Scope**: Extension（既存タイムコード受信システムにジェネレーター機能を追加）
- **Key Findings**:
  - ITimecodeEngineは既にTimecodeSourceType enumとActiveSourceプロパティを持ち、ソース切り替えの拡張ポイントが存在する
  - LtcDecoderのBMCデコードロジックを反転させることでLTCエンコーダーを実装可能
  - NAudio 2.2.1はWasapiOut/WaveOutEventによるオーディオ出力をサポートしており、追加依存なしで実現可能

## Research Log

### LTCエンコーディング仕様
- **Context**: Requirement 3（LTCオーディオ出力）の実現方式調査
- **Sources Consulted**: SMPTE 12M規格、既存LtcDecoderのBMC実装
- **Findings**:
  - LTCフレームは80ビットで構成（タイムコードBCDデータ + sync word 0xBFFC）
  - Biphase Mark Coding: ビット'1'はセル内に2回遷移、ビット'0'はセル境界で1回遷移
  - 既存LtcDecoderのビット配置（bits 0-3: frame units, 8-9: frame tens等）をそのままエンコードに流用可能
  - サンプルレート÷(80×fps) = 1ビットあたりのサンプル数
- **Implications**: LtcEncoderはLtcDecoderの逆変換として実装。同一ビット配置定数を共有できる

### NAudioオーディオ出力パターン
- **Context**: LTCオーディオ信号の出力デバイス選択と再生方式
- **Sources Consulted**: NAudio 2.2.1 API、既存WasapiCapture利用パターン
- **Findings**:
  - `WasapiOut`クラスでWASAPI排他/共有モードの出力が可能
  - `IWaveProvider`インターフェースを実装したカスタムプロバイダーでリアルタイム信号生成
  - `MMDeviceEnumerator`でDataFlow.Render指定により出力デバイス列挙（入力と対称的）
  - 既存入力パターン（WasapiCapture + DataAvailable）の対称として、WasapiOut + IWaveProvider が自然
- **Implications**: 新規ライブラリ不要。NAudioの既存依存内で完結

### TimecodeEngine拡張方式
- **Context**: 内部生成タイムコードを既存エンジンに統合する方式の検討
- **Sources Consulted**: ITimecodeEngine定義、TimecodeEngine実装、TimecodeSourceType enum
- **Findings**:
  - TimecodeSourceType enumは現在`Ltc`のみ。`Generator`を追加するのが自然
  - TimecodeEngineはStartLtc()で音声入力を開始。ジェネレーター用のStartGenerator()メソッドを追加
  - ProcessFrame()パイプライン（オフセット適用→イベント発火）はソースに依存しないため再利用可能
  - Channel<TimecodeValue>アーキテクチャはジェネレーターからのフレーム注入にもそのまま利用可能
- **Implications**: TimecodeEngineに生成モードを追加する方式が最もシンプル。別エンジン作成は不要

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| TimecodeEngine拡張 | 既存エンジンにジェネレーターモードを追加 | 統一インターフェース、既存パイプライン再利用、DI変更不要 | エンジンの責務が増大 | 選択：責務増大は限定的で管理可能 |
| 別サービス分離 | ITimecodeGeneratorとして独立サービス作成 | 責務分離が明確 | キュー・リレーとの連携に追加統合レイヤーが必要 | 不採用：オーバーエンジニアリング |

## Design Decisions

### Decision: TimecodeEngine内にジェネレーター統合
- **Context**: 生成タイムコードを既存のキュートリガー・OSCリレーとシームレスに連携させる必要がある
- **Alternatives Considered**:
  1. TimecodeEngineを拡張し、LTC受信とジェネレーターの両モードをサポート
  2. ITimecodeGeneratorを独立サービスとして作成し、オーケストレーション層で統合
- **Selected Approach**: Option 1 — TimecodeEngine拡張
- **Rationale**: ProcessFrame()パイプラインとChannel<TimecodeValue>アーキテクチャがソース非依存。既存のCueManager/TimecodeRelayはITimecodeEngineのイベントのみを購読しており、変更不要
- **Trade-offs**: エンジンのコード量が増えるが、外部からの振る舞いは統一的。ジェネレーター固有ロジック（タイマー、LTCエンコード）は専用クラスに委譲
- **Follow-up**: TimecodeEngineのユニットテストにジェネレーターモードのテストケースを追加

### Decision: LTCエンコーダーを専用クラスとして実装
- **Context**: LTC信号生成はLtcDecoderと対称的だが、責務が異なる
- **Selected Approach**: LtcEncoderクラスを新規作成
- **Rationale**: エンコードは「TimecodeValue → PCMサンプル」の変換。デコーダーの逆変換として独立した責務
- **Trade-offs**: ビット配置定数の重複可能性。共通定数クラス抽出は将来のリファクタリングとして許容

### Decision: ジェネレーターのタイミング制御にSystem.Threading.PeriodicTimerを使用
- **Context**: フレーム精度のタイムコード生成にはフレームレート同期のタイマーが必要
- **Selected Approach**: PeriodicTimerによるフレーム間隔タイミング + Stopwatchによるドリフト補正
- **Rationale**: .NET 8のPeriodicTimerはasync対応で低オーバーヘッド。Stopwatchでの経過時間追跡により累積ドリフトを防止
- **Trade-offs**: Windowsタイマー解像度（~15.6ms）は30fpsの1フレーム（~33.3ms）より十分小さいが、高フレームレートでは精度に注意

## Risks & Mitigations
- **タイマー精度**: Windowsデフォルトタイマー解像度（15.6ms）→ Stopwatchベースの累積時間追跡で補正
- **オーディオデバイス競合**: 入力と出力で同一デバイスを使用した場合の競合 → UIで入出力デバイスを分離表示
- **スレッドセーフティ**: ジェネレータースレッドとUIスレッドの競合 → 既存TimecodeEngineのlock + Channel パターンを踏襲
