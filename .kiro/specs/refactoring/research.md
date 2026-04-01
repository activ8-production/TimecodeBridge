# リサーチ & 設計判断ログ

## サマリー
- **機能**: refactoring
- **ディスカバリー範囲**: Extension（既存システムの内部構造改善）
- **主要な発見**:
  - TimecodeEngineは530行で5つの論理単位（LTCキャプチャ、ジェネレーター、フリーラン、ワーカー、信号喪失検出）を内包しており、個別クラスへの抽出が適切
  - 全ViewModelがIDisposable未実装でイベント購読の解除なし。現在はSingleton運用で問題ないが、Transient登録のViewModelでは潜在的メモリリーク
  - ProjectServiceが3つの異なる関心事（プロジェクトI/O、最近のプロジェクト管理、背景設定永続化）を1クラスに混在させている

## リサーチログ

### TimecodeEngine内部構造の分析
- **コンテキスト**: Requirement 2（TimecodeEngineの内部構造整理）の設計判断
- **ソース**: `src/TimecodeBridge/Services/TimecodeEngine.cs`（530行）
- **発見**:
  - LTCキャプチャ処理（StartLtc/StopLtcCapture）: NAudioデバイス初期化、LtcDecoder管理、DataAvailableイベントハンドリング
  - ジェネレーター処理（StartGenerator/ResumeGenerator/StopGenerator/ResetGenerator/DisposeGenerator）: TimecodeGenerator/LtcEncoder/WasapiOut管理
  - フリーラン処理（StartFreerun/StopFreerun/OnSignalLossTimeout）: 独自スレッドによるフレーム補完、有効期限タイマー
  - ワーカースレッド（WorkerLoop/ProcessFrame）: Channel<TimecodeValue>ベースのフレームパイプライン
  - すべての処理がWriteFrame()→Channel→WorkerLoop→ProcessFrameの共通パイプラインを経由
- **含意**: LTCキャプチャ、ジェネレーター、フリーランの3つは独立した関心事として抽出可能。ワーカースレッドとフレームパイプラインはTimecodeEngine自体のオーケストレーション責務として残す

### TimecodeViewModelの責務分析
- **コンテキスト**: Requirement 1（TimecodeViewModelの責務分離）の設計判断
- **ソース**: `src/TimecodeBridge/ViewModels/TimecodeViewModel.cs`（325行）
- **発見**:
  - オーディオデバイス列挙（RefreshAudioDevices）: NAudio.CoreAudioApi.MMDeviceEnumeratorを直接使用し、Capture/Render両方のデバイスを列挙
  - ジェネレーター状態管理: _generatorInitialized, IsGeneratorRunning, IsLtcOutputActiveなどの状態フラグとStart/Stop/Resetコマンド
  - LTCキャプチャ状態管理: _hasEverReceived, SelectedDevice, SelectedSourceによるソース切り替えとエンジンへの委譲
  - 設定保存/復元: GetSourceSettings/RestoreSourceSettingsでTimecodeSourceSettingsとの相互変換
- **含意**: デバイス列挙はサービスとして抽出可能。ジェネレーター/LTC状態管理はエンジンと密結合しているためViewModelに残すのが妥当

### ProjectServiceの関心事分析
- **コンテキスト**: Requirement 3（ProjectServiceの関心事分離）の設計判断
- **ソース**: `src/TimecodeBridge/Services/ProjectService.cs`（196行）
- **発見**:
  - プロジェクトI/O: LoadProject/SaveProject（JSON直列化/逆直列化）
  - 最近のプロジェクト管理: AddToRecentProjects/LoadRecentProjectsFromDisk/SaveRecentProjectsToDisk（MRUリスト、最大10件）
  - 背景設定永続化: LoadBackgroundSettings/SaveBackgroundSettings（AppSettingsのサブプロパティとしてJSON保存）
  - AppSettings内部クラスが最近のプロジェクトと背景設定を同じJSONファイルに混在保存
- **含意**: IProjectServiceインターフェースを分割し、各関心事を専用サービスに移行

### ダイアログ処理パターンの分析
- **コンテキスト**: Requirement 5（ダイアログ処理のサービス化）の設計判断
- **ソース**: CueListViewModel.cs, HostManagerViewModel.cs
- **発見**:
  - CueListViewModel: `Func<Cue, IReadOnlyList<OscHost>, FrameRate, string, Cue?>` 型のShowCueEditDialogプロパティ（テスト時に差し替え可能）
  - HostManagerViewModel: `Func<OscHost, OscHost?>` 型のShowHostEditDialogプロパティ
  - BatchDuplicateDialog: CueListViewModel内で直接`new BatchDuplicateDialog()`を生成
  - いずれもApplication.Current.MainWindowをOwnerに設定するWPF依存コード
- **含意**: Func委譲パターンからインターフェースベースのダイアログサービスに移行。テスト互換性を維持しつつ、DIによる注入を実現

### エラーハンドリングの現状分析
- **コンテキスト**: Requirement 6（エラーハンドリングの改善）の設計判断
- **ソース**: TimecodeEngine.cs, ProjectService.cs
- **発見**:
  - TimecodeEngine: 4箇所の空キャッチ（ResumeGenerator, StopGenerator, DisposeGenerator, StopLtcCapture）。いずれもNAudioデバイス操作のクリーンアップ時
  - ProjectService: 4箇所の空キャッチ（LoadRecentProjectsFromDisk, SaveRecentProjectsToDisk, LoadBackgroundSettings, SaveBackgroundSettings）。設定ファイルI/O
  - 意図的なキャッチ: OperationCanceledException, ChannelClosedException（ワーカースレッド正常終了）
  - ログ出力: Debug.WriteLineのみ（リリースビルドでは無効）
- **含意**: 空キャッチを具体的な例外型のキャッチ+ログ出力に置き換え。グレースフルデグラデーション方針は維持

### イベント購読管理の分析
- **コンテキスト**: Requirement 4（ViewModelのイベント購読管理）の設計判断
- **ソース**: 全ViewModel、ServiceRegistration.cs
- **発見**:
  - イベント購読するViewModel: TimecodeViewModel(2), CueListViewModel(2), HostManagerViewModel(1), MainViewModel(1), RelayViewModel(1), LogViewModel(1), AudioWaveformViewModel(1)
  - 全ViewModel: IDisposable未実装、イベント解除なし
  - DI登録: Singleton（TimecodeVM, CueListVM, RelayVM）、Transient（MainVM, HostManagerVM, LogVM, AudioWaveformVM）
  - Transient VMがイベント購読する場合、GCされてもイベントハンドラが残留する可能性
- **含意**: 全ViewModelにIDisposableを実装。特にTransient登録のViewModelは確実にDisposeを呼び出す仕組みが必要

## アーキテクチャパターン評価

| オプション | 説明 | 強み | リスク・制限 | 備考 |
|-----------|------|------|-------------|------|
| Facadeパターン（TimecodeEngine） | Engine内部を独立クラスに抽出し、Engineをオーケストレーターとする | テスト容易、責務明確 | 内部クラス間の状態同期が必要 | 既存のITimecodeEngineインターフェースを維持可能 |
| インターフェース分離（ProjectService） | IProjectServiceを分割し、各関心事に専用インターフェースを提供 | 依存関係が明確、テスト容易 | 既存コードの参照箇所を更新必要 | ISP原則に従う |
| サービスインターフェース（ダイアログ） | IDialogServiceインターフェースでダイアログ表示を抽象化 | テスト容易、WPF依存を分離 | 型安全性の確保が必要 | Func委譲の代替 |

## 設計判断

### Decision: TimecodeEngine内部クラスの抽出戦略
- **コンテキスト**: TimecodeEngineが530行で複数の処理パイプラインを内包
- **検討した代替案**:
  1. 内部クラス（nested class）として抽出 — カプセル化は保つが外部からテスト不可
  2. 独立クラス+内部コンストラクタ — テスト可能かつアセンブリ内に限定
  3. インターフェース経由の独立サービス — 完全に分離、DI登録
- **選択**: オプション2（独立クラス+internal可視性）。TimecodeEngine内で直接生成・管理し、DIには登録しない
- **根拠**: 抽出クラスはTimecodeEngineの内部実装詳細であり、外部から直接利用する必要がない。InternalsVisibleToでテストプロジェクトからアクセス可能
- **トレードオフ**: DIの恩恵は受けないが、不要な公開APIの増加を防ぐ
- **フォローアップ**: テストプロジェクトにInternalsVisibleTo属性を追加

### Decision: ProjectServiceの分割粒度
- **コンテキスト**: ProjectServiceが3つの関心事を持つ
- **検討した代替案**:
  1. 3サービスに完全分割（ProjectService, RecentProjectsService, AppSettingsService）
  2. 2サービスに分割（ProjectService, AppSettingsService）— 最近のプロジェクトをAppSettings内に
  3. 現状維持+内部リファクタリングのみ
- **選択**: オプション1（3サービスに完全分割）
- **根拠**: 各サービスが単一責任を持ち、テストが容易になる。最近のプロジェクト管理と背景設定は異なるライフサイクルと変更理由を持つ
- **トレードオフ**: DI登録が増加するが、各サービスの複雑度は大幅に低下
- **フォローアップ**: AppSettingsの共有JSONファイルの読み書き競合を考慮（両サービスが同一ファイルを操作）

### Decision: ダイアログサービスの設計方針
- **コンテキスト**: CueListViewModel/HostManagerViewModelがFunc委譲でダイアログを表示
- **検討した代替案**:
  1. 汎用IDialogService（ShowDialog<T>メソッド）
  2. 用途別インターフェース（ICueDialogService, IHostDialogService）
  3. 現行Func委譲パターンの維持
- **選択**: オプション2（用途別インターフェース）
- **根拠**: 型安全性が高く、各ダイアログの入出力が明確に定義される。汎用インターフェースは型安全性と引き換えに柔軟性を得るが、ダイアログの種類が限定的な本プロジェクトでは不要
- **トレードオフ**: ダイアログ種類が増えるとインターフェースも増えるが、現時点では3種類（Cue編集、Host編集、バッチ複製）のみ
- **フォローアップ**: ファイルダイアログ（開く・保存）も対象に含めるかは要件5.2に基づく

## リスク & 緩和策
- **リスク1**: 内部クラス抽出時のスレッド安全性の破壊 — 緩和: 共有状態へのアクセスパターンを明確に定義し、ロック戦略を文書化
- **リスク2**: IProjectServiceインターフェース変更による既存テストの大量修正 — 緩和: 段階的に移行し、旧インターフェースから新インターフェースへのアダプターを一時的に提供
- **リスク3**: イベント解除の実装漏れによるメモリリーク残留 — 緩和: IDisposableの実装パターンを統一し、テストで検証

## リファレンス
- [CommunityToolkit.Mvvm ドキュメント](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) — ObservableObject, RelayCommandの使用パターン
- [Microsoft.Extensions.DependencyInjection ドキュメント](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection) — サービスライフタイム管理
- [NAudio ドキュメント](https://github.com/naudio/NAudio) — WasapiCapture/WasapiOutのDispose要件
