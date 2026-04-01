# Implementation Plan

- [x] 1. TimecodeEngine内部クラスの抽出
- [x] 1.1 (P) LTCキャプチャ制御の独立クラス化
  - TimecodeEngineからLTCキャプチャデバイスの初期化・管理・停止ロジックを抽出し、独立したinternalクラスを作成する
  - WasapiCapture/WasapiLoopbackCaptureのライフサイクル管理（初期化、開始、停止、破棄）を移管する
  - LtcDecoderへのサンプルデータ中継とデコード結果のコールバック通知を実装する
  - 波形表示用オーディオサンプルの抽出・通知機能を含める
  - RecordingStopped同期パターン（ManualResetEventSlim）を維持する
  - IDisposableを実装し、リソースの確実な解放を保証する
  - _Requirements: 1.3, 2.1_

- [x] 1.2 (P) ジェネレーター制御の独立クラス化
  - TimecodeEngineからジェネレーターの開始・一時停止・再開・リセット・破棄ロジックを抽出し、独立したinternalクラスを作成する
  - TimecodeGenerator/LtcEncoderへのフレーム供給ロジックを移管する
  - 音声出力デバイス（WasapiOut）の初期化・管理をオプション機能として実装する（デバイスなしでもフレーム生成は継続）
  - IDisposableを実装し、リソースの確実な解放を保証する
  - _Requirements: 1.2, 2.2_

- [x] 1.3 (P) フリーラン制御の独立クラス化
  - TimecodeEngineから信号喪失時のフレーム自動補完ロジックを抽出し、独立したinternalクラスを作成する
  - 最終受信フレームからの連続フレーム生成ロジックを移管する
  - Stopwatchベースの精密タイミング制御と指定時間経過後の自動停止を含める
  - 専用スレッド+CancellationTokenパターンを維持する
  - IDisposableを実装し、スレッド終了を保証する
  - _Requirements: 2.3_

- [x] 1.4 TimecodeEngineをオーケストレーターに再構成
  - 抽出した3つの内部クラスをTimecodeEngine内で生成・統合し、Facadeパターンで構成する
  - ITimecodeEngineインターフェースは変更せず、既存の公開APIを維持する
  - Channel<TimecodeValue>ベースのフレームパイプラインを維持する
  - 各内部クラスのコールバックをTimecodeEngineのイベント・パイプラインに接続する
  - TimecodeEngine自身のビジネスロジックを排除し、調整役に徹する
  - 既存テストが全て通過することを確認する
  - _Requirements: 1.5, 2.4, 2.5_

- [x] 2. サービス層の新規抽出
- [x] 2.1 (P) オーディオデバイスサービスの作成
  - TimecodeViewModelからオーディオデバイス列挙ロジックを抽出し、公開インターフェースと実装クラスを作成する
  - キャプチャデバイス（入力）とレンダーデバイス（出力/ループバック）の列挙機能を実装する
  - ループバック用のデバイス名加工（" (Loopback)"付加）をサービス内で処理する
  - MMDeviceEnumeratorの例外（COMException等）をキャッチし、ログ出力して空リストを返却する
  - DIコンテナにSingletonとして登録する
  - _Requirements: 1.1_

- [x] 2.2 (P) アプリケーション設定サービスの作成
  - ProjectServiceから設定の読み書きロジックを抽出し、公開インターフェースと実装クラスを作成する
  - 背景画像設定の読み書き機能を実装する
  - 最近使用したプロジェクトリストの読み書き機能を実装する
  - settings.jsonファイルへのJSON永続化を実装する
  - ファイル破損時のデフォルト値フォールバック、I/Oエラー時のグレースフルハンドリングを含める
  - DIコンテナにSingletonとして登録する
  - _Requirements: 3.3_

- [x] 2.3 最近のプロジェクト管理サービスの作成
  - ProjectServiceからMRUリスト管理ロジックを抽出し、公開インターフェースと実装クラスを作成する
  - アプリケーション設定サービスを経由した永続化を行う
  - MRUリストの最大件数制限（10件）と最新使用順の維持を実装する
  - 永続化失敗時はメモリ内リストを維持するベストエフォート方式とする
  - DIコンテナにSingletonとして登録する
  - 2.2のアプリケーション設定サービスに依存する
  - _Requirements: 3.2_

- [x] 3. ProjectServiceの再構成
- [x] 3.1 ProjectServiceをプロジェクトファイルI/O専任に縮小
  - ProjectServiceから設定管理・MRUリスト管理を除去し、プロジェクトファイルの読み書きのみに責務を限定する
  - IProjectServiceインターフェースを縮小再定義する
  - 既存のプロジェクトファイル形式と動作を維持する
  - ProjectServiceを使用しているViewModelの依存関係を新サービスに切り替える
  - プロジェクト保存・読み込みのラウンドトリップが正常に動作することを確認する
  - _Requirements: 3.1, 3.4_

- [x] 4. ダイアログサービスの抽出
- [x] 4.1 (P) キュー編集ダイアログサービスの作成
  - CueListViewModelからキュー編集・バッチ複製ダイアログの表示ロジックを抽出し、公開インターフェースと実装クラスを作成する
  - CueListViewModelのFunc委譲をサービスインターフェース経由に置き換える
  - モーダルダイアログの所有者設定（Application.Current?.MainWindow）を維持する
  - DIコンテナにSingletonとして登録する
  - _Requirements: 5.1, 5.3_

- [x] 4.2 (P) ホスト編集ダイアログサービスの作成
  - HostManagerViewModelからホスト編集ダイアログの表示ロジックを抽出し、公開インターフェースと実装クラスを作成する
  - HostManagerViewModelのFunc委譲をサービスインターフェース経由に置き換える
  - DIコンテナにSingletonとして登録する
  - _Requirements: 5.1, 5.3_

- [x] 4.3 (P) ファイルダイアログサービスの作成
  - コードビハインドからファイルダイアログ（開く・保存）の表示ロジックを抽出し、公開インターフェースと実装クラスを作成する
  - MainWindowのコードビハインドからダイアログ依存を軽減する
  - DIコンテナにSingletonとして登録する
  - _Requirements: 5.2_

- [x] 4.4 ダイアログサービスのViewModel統合
  - 各ViewModelのダイアログ呼び出しを新サービス経由に切り替える
  - リファクタリング前と同一のユーザー体験が維持されることを確認する
  - _Requirements: 5.4_

- [x] 5. TimecodeViewModelの責務再構成
- [x] 5.1 TimecodeViewModelからビジネスロジックを除去
  - オーディオデバイス列挙をオーディオデバイスサービスに委譲する
  - ジェネレーター状態管理をTimecodeEngine（内部のGeneratorController）に委譲する
  - LTCキャプチャ状態管理をTimecodeEngine（内部のLtcCaptureController）に委譲する
  - TimecodeViewModelをUIバインディングとコマンドのみに限定する
  - リファクタリング前と同一の外部動作を維持する
  - _Requirements: 1.1, 1.4, 1.5_

- [x] 6. ViewModelのイベント購読管理
- [x] 6.1 全ViewModelにIDisposableを実装
  - TimecodeViewModel、CueListViewModel、HostManagerViewModel、MainViewModelにIDisposableを実装する
  - 各ViewModelが購読しているイベントハンドラをDispose時に解除する
  - DIコンテナからのViewModelライフサイクル管理（アプリ終了時のDispose呼び出し）を設定する
  - _Requirements: 4.1, 4.2, 4.3_

- [x] 7. エラーハンドリングの改善
- [x] 7.1 (P) TimecodeEngine関連の空キャッチブロック排除
  - TimecodeEngine（およびEngine内部クラス）の空キャッチブロックを特定し、具体的な例外型の指定とログ出力を追加する
  - グレースフルデグラデーション方針を維持しつつ、問題の可視性を向上させる
  - _Requirements: 6.1, 6.2, 6.3_

- [x] 7.2 (P) ProjectService関連の空キャッチブロック排除
  - ProjectService（および新規抽出サービス）の空キャッチブロックを特定し、具体的な例外型の指定とログ出力を追加する
  - グレースフルデグラデーション方針を維持しつつ、問題の可視性を向上させる
  - _Requirements: 6.1, 6.2, 6.3_

- [x] 8. テストの維持と拡充
- [x] 8.1 既存テストの全通過確認
  - 全リファクタリング完了後に既存テストスイートを実行し、全テストが通過することを確認する
  - InternalsVisibleToをテストプロジェクトに追加し、Engine内部クラスのテストを可能にする
  - _Requirements: 7.1_

- [x] 8.2 新規抽出サービスのユニットテスト追加
  - Engine内部クラス（LtcCaptureController、GeneratorController、FreerunController）のライフサイクル・コールバックテストを追加する
  - AudioDeviceService、RecentProjectsService、AppSettingsServiceのユニットテストを追加する
  - ダイアログサービスのモック実装を用いたViewModel統合テストを追加する
  - _Requirements: 7.2_

- [x] 8.3 ViewModelテストの追加
  - ViewModelの主要なコマンド実行・プロパティ変更に対するテストを追加する
  - ダイアログサービスのモックを利用したテスト容易性を検証する
  - _Requirements: 7.3_
