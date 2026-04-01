# ギャップ分析: リファクタリング

## 分析サマリー

- **TimecodeViewModel** は324行で、オーディオデバイス列挙（NAudio直接依存）、ジェネレーター状態管理、LTCキャプチャ状態管理、UIバインディングが混在しており、責務分離の必要性が高い
- **TimecodeEngine** は529行の最大クラスで、LTCキャプチャ・ジェネレーター制御・フリーラン・ワーカースレッド管理が一体化しており、内部クラス抽出の余地が大きい
- **ProjectService** はプロジェクトI/O・最近使用リスト管理・背景設定永続化の3つの関心事を内包しており、分離が比較的容易
- **全ViewModel** でイベント購読解除（`-=`）が未実装であり、IDisposable実装が必要（現在IDisposableを実装するViewModelはゼロ）
- **ダイアログ処理** はCueListViewModelとHostManagerViewModelで`Func`委譲パターンを採用済みだが、CueListViewでのBatchDuplicateDialogとMainWindow.xaml.csでのファイルダイアログはコードビハインド直接呼出し

---

## 1. 要件別アセットマッピング

### Requirement 1: TimecodeViewModelの責務分離

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| オーディオデバイス列挙・キャッシュ | `TimecodeViewModel.RefreshAudioDevices()` (L126-146) — NAudio `MMDeviceEnumerator` を直接使用 | **Missing**: 専用サービスクラスが存在しない |
| ジェネレーター状態管理 | `TimecodeViewModel` 内の `_generatorInitialized`, `StartGenerator()`, `StopGenerator()`, `ResetGenerator()` | **Missing**: 状態管理ロジックがVM内にインライン |
| LTCキャプチャ状態管理 | `TimecodeViewModel.OnSelectedDeviceChanged()`, `OnSelectedSourceChanged()` 内のエンジン呼出し | **Missing**: キャプチャ制御のオーケストレーションがVM内 |
| UIバインディング専用化 | `TimecodeViewModel` は `DispatcherViewModel` 継承、CommunityToolkit.Mvvm使用 | **Constraint**: 既存のバインディングプロパティ名・コマンド名を維持する必要あり |

### Requirement 2: TimecodeEngineの内部構造整理

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| LTCキャプチャ抽出 | `TimecodeEngine.StartLtc()`, `StopLtcCapture()`, `_wasapiCapture`, `_ltcDecoder` (L111-304) | **Missing**: 独立クラスなし。`WasapiCapture`の`RecordingStopped`イベント待機ロジックも含む |
| ジェネレーター抽出 | `TimecodeEngine.StartGenerator()`, `DisposeGenerator()`, `ResumeGenerator()` (L163-284) | **Missing**: `TimecodeGenerator`クラスは存在するが、LTC出力（`WasapiOut`, `LtcEncoder`）との統合ロジックはEngine内 |
| フリーラン抽出 | `TimecodeEngine.StartFreerun()`, `StopFreerun()`, `OnSignalLossTimeout()` (L438-528) | **Missing**: 独立クラスなし。専用スレッドで独自のフレーム生成ループを持つ |
| オーケストレーター化 | `TimecodeEngine` 全体 | **Constraint**: `Channel<TimecodeValue>` パイプラインと `WorkerLoop` の処理はオーケストレーターに残す必要がある |

### Requirement 3: ProjectServiceの関心事分離

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| プロジェクトI/O専用化 | `ProjectService.LoadProject()`, `SaveProject()` | 既に明確な境界あり。分離は容易 |
| 最近使用リスト管理 | `ProjectService._recentProjects`, `AddToRecentProjects()`, `LoadRecentProjectsFromDisk()`, `SaveRecentProjectsToDisk()` | **Missing**: 専用サービスなし |
| アプリ設定永続化 | `ProjectService.LoadBackgroundSettings()`, `SaveBackgroundSettings()`, 内部`AppSettings`クラス | **Missing**: 専用サービスなし。`AppSettings`がprivateクラスとして内包 |
| IProjectServiceインターフェース | `IProjectService` に全メソッドが混在 | **Constraint**: インターフェース分割時に既存の利用箇所（MainViewModel, ServiceRegistration, テスト）の更新が必要 |

### Requirement 4: ViewModelのイベント購読管理

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| IDisposable実装 | `DispatcherViewModel` 基底クラスあり | **Missing**: ViewModelのいずれもIDisposable未実装 |
| イベント解除 | 購読箇所: TimecodeVM(2件), CueListVM(2件), RelayVM(1件,ラムダ), LogVM(1件), MainVM(1件), HostManagerVM(1件), AudioWaveformVM(1件) | **Missing**: `-=` による解除処理が一切存在しない |
| DIコンテナ統合 | `ServiceRegistration` でSingleton/Transient登録 | **Constraint**: Singletonは破棄タイミングが`ServiceProvider.Dispose()`時。Transientは手動管理が必要。現在`App.OnExit`でTimecodeEngineのみ明示的に破棄 |

### Requirement 5: ダイアログ処理のサービス化

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| キュー編集ダイアログ | `CueListViewModel.ShowCueEditDialog` — `Func<Cue, IReadOnlyList<OscHost>, FrameRate, string, Cue?>` 委譲 | **Partial**: Funcベースの委譲は機能するが、インターフェース化されていない |
| ホスト編集ダイアログ | `HostManagerViewModel.ShowHostEditDialog` — `Func<OscHost, OscHost?>` 委譲 | **Partial**: 同上 |
| バッチ複製ダイアログ | `CueListViewModel.BatchDuplicateCue()` 内で直接 `new BatchDuplicateDialog()` | **Missing**: 委譲なし、コードビハインド直接 |
| ファイルダイアログ | `MainWindow.xaml.cs` のコードビハインド (`OpenFileDialog`, `SaveFileDialog`) | **Missing**: サービスインターフェースなし |

### Requirement 6: エラーハンドリングの改善

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| 空catchブロック排除 | TimecodeEngine: 3箇所の `catch { /* ignore */ }` (L213, 226, 274, 290)、ProjectService: 4箇所の bare `catch` (L111, 133, 150, 184)、MainWindow: 1箇所 (L87) | **Missing**: ログ出力がない。ProjectServiceはbare catchで例外型未指定 |
| 具体的例外型指定 | 一部は`Exception ex`をキャッチ済み | **Missing**: `catch`（型指定なし）が複数箇所 |
| ロギング基盤 | `System.Diagnostics.Debug.WriteLine` を一部使用 | **Constraint**: 構造化ロギングフレームワーク（Serilog等）未導入。Research Needed: ロギング戦略の決定 |

### Requirement 7: 既存テストの維持と拡充

| 技術ニーズ | 既存アセット | ギャップ |
|-----------|-------------|---------|
| 既存テスト | 25テストファイル: Models(4), Services(8), ViewModels(6), Integration(4), Infrastructure(1), Themes(1) | テストカバレッジは良好 |
| 新規抽出クラスのテスト | — | **Missing**: 抽出予定のクラス（AudioDeviceService, FreerunManager等）にはテストが未存在（当然） |
| VM Disposeテスト | — | **Missing**: IDisposable実装後のテストが必要 |

---

## 2. 実装アプローチの選択肢

### Option A: 段階的抽出（推奨）

**概要**: 各Requirementを独立したフェーズとして順番に実施。各フェーズでテスト通過を確認してから次へ進む。

**実施順序**:
1. Req 6 (エラーハンドリング) — 最小変更、リスク低
2. Req 3 (ProjectService分離) — 独立性高、他への影響小
3. Req 4 (IDisposable) — 基盤整備
4. Req 2 (TimecodeEngine分離) — 最大クラスの構造改善
5. Req 1 (TimecodeViewModel分離) — Engine依存のため後
6. Req 5 (ダイアログサービス化) — UI層の改善
7. Req 7 (テスト拡充) — 各フェーズ内でも実施するが、最終仕上げ

**トレードオフ**:
- ✅ 各ステップでテスト可能、デグレッション検出が早い
- ✅ 各フェーズが独立したコミットになり、問題時のrevertが容易
- ❌ 全体の工数がやや多い（インターフェース変更が段階的に波及）

### Option B: 一括リファクタリング

**概要**: 全Requirementを一度に実施。

**トレードオフ**:
- ✅ インターフェース変更を一度にまとめられる
- ❌ 変更量が大きく、問題の切り分けが困難
- ❌ テスト全壊時の復旧が困難

### Option C: ハイブリッド（グループ化）

**概要**: 関連するRequirementをグループ化して2-3フェーズで実施。

**グループ1**: Req 6 + Req 4 (基盤改善: エラーハンドリング + Dispose)
**グループ2**: Req 2 + Req 1 + Req 3 (構造改善: Engine + ViewModel + ProjectService)
**グループ3**: Req 5 + Req 7 (UI + テスト最終化)

**トレードオフ**:
- ✅ フェーズ数を削減しつつ、関連変更をまとめられる
- ✅ 各グループ内で整合性を保ちやすい
- ❌ グループ2の変更量が大きい

---

## 3. 複雑度とリスク評価

| Requirement | 工数 | リスク | 根拠 |
|-------------|------|--------|------|
| Req 1: TimecodeViewModel分離 | M (3-7日) | Medium | NAudio依存の抽出は新パターンだが既存テストで検証可能 |
| Req 2: TimecodeEngine分離 | L (1-2週) | Medium | 最大クラスで並行処理を含む。内部クラス抽出はChannel/Thread管理との統合が要注意 |
| Req 3: ProjectService分離 | S (1-3日) | Low | 明確な責務境界あり。既存テストでファイルI/Oを検証済み |
| Req 4: IDisposable実装 | M (3-7日) | Medium | 7つのViewModelにDispose追加。DIコンテナのライフサイクル管理の設計判断が必要 |
| Req 5: ダイアログサービス化 | S (1-3日) | Low | 既存Func委譲パターンをインターフェースに昇格。BatchDuplicateDialogの委譲追加 |
| Req 6: エラーハンドリング | S (1-3日) | Low | 機械的な変更。既存のグレースフルデグラデーション方針を維持 |
| Req 7: テスト拡充 | M (3-7日) | Low | 既存テスト基盤が充実しており、パターンに従うだけ |

**全体工数**: L (1-2週間)
**全体リスク**: Medium — 並行処理コード（TimecodeEngine）の分解が最大のリスク要因

---

## 4. デザインフェーズへの推奨事項

### 推奨アプローチ
**Option A（段階的抽出）** を推奨。最も安全で、各ステップでテスト通過を確認できる。

### 主要な設計判断事項
1. **TimecodeEngine分解時の所有権**: `Channel<TimecodeValue>` のWriter権限を抽出クラスにどう渡すか（コールバック vs インターフェース）
2. **IDisposable戦略**: ViewModelのDispose呼出しをどう保証するか（DIライフサイクル vs 明示的管理）
3. **ダイアログサービスのスコープ**: 単一`IDialogService`にまとめるか、機能別に分割するか

### Research Needed
- **ロギング戦略**: `System.Diagnostics.Debug.WriteLine` を維持するか、ILogger導入を検討するか（Req 6のスコープ決定に影響）
- **RelayViewModelのラムダ購読**: `_hostRegistry.HostChanged += (_, _) => RefreshHostSelections()` はラムダのため`-=`で解除不可。パターン変更が必要
