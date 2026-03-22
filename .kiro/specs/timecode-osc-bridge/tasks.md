# Implementation Plan

- [x] 1. プロジェクトスキャフォールディングと基盤構築
- [x] 1.1 WPF アプリケーションプロジェクトの作成と NuGet パッケージの導入
  - .NET 8 / Windows x64 ターゲットで WPF アプリケーションプロジェクトを作成する
  - NuGet パッケージを導入する: CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, NAudio, BuildSoft.OscCore, System.Text.Json
  - libltc ネイティブ DLL（x64）をプロジェクトに同梱し、ビルド時に出力ディレクトリへコピーされるよう設定する
  - App.xaml.cs に DI コンテナのセットアップを実装し、全サービスとViewModelを登録する
  - _Requirements: 6.1_

- [x] 1.2 ダークテーマの ResourceDictionary を作成する
  - ダークテーマ用のカラーパレット（背景、前景、アクセント、エラー色）を定義する
  - 共通コントロール（Button, TextBox, ListView, CheckBox 等）のスタイルテンプレートを作成する
  - App.xaml にテーマリソースを適用する
  - _Requirements: 6.6_

- [x] 2. ドメインモデルの実装
- [x] 2.1 Timecode 関連の値型を実装する
  - TimecodeValue を不変構造体として実装し、Hours/Minutes/Seconds/Frames プロパティ、TotalFrames 算出、オフセット加算メソッドを提供する
  - FrameRate 列挙型（Fps24, Fps25, Fps2997Drop, Fps30）を定義する
  - TimecodeOffset を不変構造体として実装し、±HH:MM:SS:FF の表現と内部フレーム数演算を提供する
  - TimecodeValue のフレーム演算・オフセット加算・境界値処理のユニットテストを作成する
  - _Requirements: 1.4, 1.7_

- [x] 2.2 (P) OSC 関連モデルとホストモデルを実装する
  - OscArgument を抽象基底クラスとし、OscInt32 / OscFloat32 / OscString の判別共用体パターンで実装する
  - OscHost モデル（Id, Name, IpAddress, Port, IsEnabled）を実装する
  - Cue モデル（Id, Name, Memo, TriggerTime, OscAddress, Arguments, TargetHostIds, IsEnabled）を実装する
  - _Requirements: 3.3, 5.2, 2.2, 2.3, 2.4, 2.5_

- [x] 2.3 (P) プロジェクトデータモデルとシリアライズ設定を実装する
  - ProjectData モデル（Cues, Hosts, RelaySettings, Offset, SourceSettings）を実装する
  - RelaySettings モデル（OscAddressPattern, ContinuousInterval, TargetHostIds, IsContinuousEnabled）を実装する
  - OscArgument の判別共用体に対応した JSON カスタムコンバーターを実装する
  - ProjectData の JSON シリアライズ/デシリアライズのユニットテストを作成する
  - _Requirements: 7.1_

- [x] 3. HostRegistry サービスの実装
- [x] 3.1 (P) ホストの CRUD 操作と変更通知を実装する
  - IHostRegistry インターフェースに従い、ホストの追加・更新・削除・有効/無効切替を実装する
  - ID ベースでホストを管理し、GetEnabledHosts で指定 ID リストから有効なホストのみを返す
  - HostChanged イベントで変更通知を発行し、参照側（キュー・リレー）に自動反映する
  - CRUD 操作とイベント通知のユニットテストを作成する
  - _Requirements: 5.2, 5.4, 5.5, 3.2, 3.6_

- [x] 4. OscSender サービスの実装
- [x] 4.1 OSC メッセージの UDP 送信機能を実装する
  - IOscSender インターフェースに従い、BuildSoft.OscCore を使用した UDP 送信を実装する
  - OscArgument の判別共用体（OscInt32/OscFloat32/OscString）を OSC プロトコルの引数に変換する
  - 指定された送信先ホスト ID リストに対して、HostRegistry から有効ホストを取得し同時送信する
  - 送信失敗時にログ記録と SendCompleted イベントでエラー情報を通知する
  - 接続テスト用の SendPing 機能を実装する
  - _Requirements: 3.1, 3.3, 3.4, 3.5, 5.3_

- [x] 5. TimecodeEngine サービスの実装
- [x] 5.1 Channel ベースのスレッディング基盤と信号喪失検出を実装する
  - キャプチャスレッドからワーカースレッドへ Timecode フレームを受け渡す Channel パイプラインを構築する
  - ワーカースレッドでオフセットを適用し、TimecodeUpdated イベントを発火する
  - 最終受信から 500ms 経過で信号喪失を検出し、StatusChanged イベントで通知する
  - CurrentRawTimecode / CurrentOffsetTimecode / IsReceiving 等の状態プロパティをスレッドセーフに公開する
  - _Requirements: 1.5, 1.6, 1.7, 1.8, 1.10_

- [x] 5.2 LTC デコーダーを実装する
  - NAudio の WasapiCapture（共有モード）でオーディオ入力をキャプチャする
  - libltc ネイティブ DLL の P/Invoke 定義を作成し、オーディオサンプルから LTC フレームをデコードする
  - デコードされた Timecode フレームを Channel へ書き込む
  - StartLtc / Stop の開始・停止制御を実装する
  - _Requirements: 1.1, 1.4_

- [x] 5.3 MTC デコーダーを実装する
  - NAudio の MidiIn で MIDI 入力を受信する
  - MTC Quarter Frame メッセージ（8 メッセージで 1 フレーム完成）のパーサーを実装する
  - フレームレート情報の抽出と TimecodeValue への変換を行う
  - デコードされた Timecode フレームを Channel へ書き込む
  - StartMtc / Stop の開始・停止制御を実装する
  - _Requirements: 1.2, 1.4_

- [x] 6. CueManager サービスの実装
- [x] 6.1 キューの CRUD 操作を実装する
  - ICueManager インターフェースに従い、キューの追加・更新・削除・並び替え・有効/無効切替を実装する
  - キューリストを内部的にトリガー時間順でソートし、効率的な範囲判定を可能にする
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.7_

- [x] 6.2 Timecode ベースの範囲判定トリガーロジックを実装する
  - TimecodeEngine の TimecodeUpdated イベントを購読する
  - 前回チェック時のオフセット適用済み Timecode ～ 現在のオフセット適用済み Timecode の範囲内にトリガー時間が含まれるかで判定する
  - フレームスキップや信号復帰時のジャンプでもキューを取りこぼさない範囲判定を実装する
  - Timecode が逆行した場合（巻き戻し）は前回値をリセットし範囲判定を再開する
  - 無効キューはトリガー判定をスキップする
  - トリガー時に OscSender を呼び出し、キューに設定された送信先ホストへ OSC メッセージを送信する
  - CueTriggered イベントを発行する
  - 範囲判定ロジック（正常、スキップ、逆行、無効キュー）のユニットテストを作成する
  - _Requirements: 1.10, 2.6, 2.7, 2.8_

- [x] 6.3 手動トリガー機能を実装する
  - Timecode 受信状態に関係なく、指定されたキューの OSC メッセージを即座に送信する
  - CueTriggered イベントを発行する
  - _Requirements: 2.9, 2.10_

- [x] 7. TimecodeRelay サービスの実装
- [x] 7.1 継続送信モードを実装する
  - ITimecodeRelay インターフェースに従い、TimecodeEngine の TimecodeUpdated イベントを購読する
  - 設定された送信間隔（フレーム毎またはインターバル指定）に基づいてオフセット適用済み Timecodeを OSC 送信する
  - カスタマイズ可能な OSC アドレスパターンを使用する
  - 送信先ホストを HostRegistry から選択して送信する
  - 継続送信の有効/無効切替を実装する
  - 信号喪失時にリレー送信を停止し、送信先に通知する
  - _Requirements: 4.1, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9_

- [x] 7.2 ワンショット送信モードを実装する
  - TriggerOneShot 呼び出し時に、その瞬間のオフセット適用済み Timecode を1回だけ送信先ホストへ送信する
  - 継続送信モードと同じ OSC アドレスパターン・送信先ホスト設定を使用する
  - _Requirements: 4.2, 4.3_

- [x] 8. ProjectService の実装
- [x] 8.1 (P) プロジェクトファイルの保存・読込を実装する
  - IProjectService インターフェースに従い、ProjectData を JSON ファイルとして保存・読込する
  - 未保存変更の追跡と UnsavedChangesStatusChanged イベントを実装する
  - 最近使用したプロジェクトファイルのパス履歴を %APPDATA%/TimecodeBridge/settings.json に保存・取得する
  - 保存・読込・履歴管理のユニットテストを作成する
  - _Requirements: 7.1, 7.2, 7.4_

- [x] 9. ViewModel レイヤーの実装
- [x] 9.1 TimecodeViewModel を実装する
  - TimecodeEngine の TimecodeUpdated / StatusChanged イベントを購読し、Dispatcher で UI スレッドに同期する
  - 生の Timecode とオフセット適用済み Timecode を同時にバインディング可能なプロパティで公開する
  - オフセット値の設定用プロパティとコマンドを提供する
  - 受信状態インジケーター（受信中/停止/信号喪失）をバインディングで公開する
  - Timecode ソース（LTC/MTC）の選択とデバイス選択のコマンドを提供する
  - _Requirements: 1.3, 1.6, 1.8, 1.9_

- [x] 9.2 CueListViewModel を実装する
  - CueManager のキューリストを ObservableCollection でバインドする
  - キューの追加・編集・削除・並び替えのコマンドを提供する
  - 各キューの手動トリガーボタンのコマンドを提供する
  - 次にトリガーされるキューのハイライト状態を管理する
  - キューがトリガーされた際の視覚的フィードバック状態を管理する
  - 各キューの送信先ホスト選択 UI の状態を管理する
  - _Requirements: 2.9, 6.3, 6.4_

- [x] 9.3 (P) HostManagerViewModel を実装する
  - HostRegistry のホスト一覧を ObservableCollection でバインドする
  - ホストの追加・編集・削除・有効/無効切替のコマンドを提供する
  - 接続テスト（OSC ping）のコマンドを提供する
  - _Requirements: 5.1, 5.3_

- [x] 9.4 (P) RelayViewModel を実装する
  - TimecodeRelay の設定値（OSC アドレス、送信間隔、送信先ホスト）をバインディングで公開する
  - 継続送信モードの有効/無効切替コマンドを提供する
  - ワンショット送信のトリガーコマンドを提供する
  - _Requirements: 4.2, 4.8_

- [x] 9.5 (P) LogViewModel を実装する
  - OscSender の SendCompleted イベントを購読し、送信ログ（成功/失敗）を収集する
  - 循環バッファ（最大 1000 件）でログエントリを管理し、ObservableCollection でバインドする
  - _Requirements: 6.5_

- [x] 9.6 MainViewModel を実装する
  - プロジェクトの保存・読込・新規作成のコマンドを提供する
  - 最近使用したプロジェクト一覧を表示する
  - アプリケーション終了時に未保存変更がある場合の保存確認ロジックを実装する
  - _Requirements: 6.2, 7.3, 7.4_

- [x] 10. UI ビューの実装
- [x] 10.1 メインウィンドウのレイアウトを実装する
  - Timecode 表示、キューリスト、ホスト一覧、リレー制御、ログパネルを一画面に配置するレイアウトを構築する
  - 各 UserControl（TimecodeDisplayView, CueListView, HostManagerView, RelayControlView, LogView）を配置する
  - メニューバーにプロジェクト操作（新規/開く/保存/最近のファイル）を配置する
  - _Requirements: 6.2_

- [x] 10.2 TimecodeDisplayView を実装する
  - 生の Timecode とオフセット適用済み Timecode を大きなフォントで同時表示する
  - オフセット入力フィールド（±HH:MM:SS:FF）を配置する
  - 受信状態インジケーター（色付きアイコンまたはラベル）を表示する
  - Timecode ソース選択（LTC/MTC）とデバイス選択のコントロールを配置する
  - _Requirements: 1.3, 1.6, 1.8, 1.9_

- [x] 10.3 CueListView を実装する
  - キューリストを ListView/DataGrid で表示し、各行にトリガー時間・名前・OSC アドレス・有効/無効・手動トリガーボタンを配置する
  - キューの追加・編集・削除ボタンを配置する
  - 次にトリガーされるキューのハイライト表示を実装する
  - キュートリガー時の視覚的フィードバック（一時的な色変更アニメーション）を実装する
  - キュー編集ダイアログ（トリガー時間、OSC アドレス、引数、送信先ホスト選択、名前、メモ）を実装する
  - _Requirements: 2.1, 2.9, 6.3, 6.4_

- [x] 10.4 (P) HostManagerView を実装する
  - ホスト一覧を表示し、各行に名前・IP アドレス・ポート・有効/無効・接続テストボタンを配置する
  - ホストの追加・編集・削除ボタンを配置する
  - _Requirements: 5.1_

- [x] 10.5 (P) RelayControlView を実装する
  - 継続送信モードの有効/無効トグルを配置する
  - ワンショット送信のトリガーボタンを配置する
  - OSC アドレスパターン・送信間隔・送信先ホスト選択のコントロールを配置する
  - _Requirements: 4.2, 4.4, 4.5, 4.6, 4.8_

- [x] 10.6 (P) LogView を実装する
  - OSC 送信ログを時系列でリアルタイム表示する
  - 成功/失敗を色分けして表示する
  - ログクリアボタンを配置する
  - _Requirements: 6.5_

- [x] 11. アプリケーション統合と終了処理
- [x] 11.1 DI コンテナへの全サービス・ViewModel の登録と起動フローを完成させる
  - App.xaml.cs で全サービスと ViewModel を DI コンテナに登録する
  - MainWindow の DataContext に MainViewModel を注入する
  - 各 UserControl の DataContext を適切な ViewModel にバインドする
  - アプリケーション終了時に TimecodeEngine の停止と未保存変更の確認ダイアログを表示する
  - _Requirements: 6.1, 7.3_

- [x] 12. 統合テスト
- [x] 12.1 キュートリガーフローの統合テストを作成する
  - TimecodeEngine にシミュレートされた Timecode を入力し、CueManager がキューをトリガーし、OscSender が正しいホストへ送信することを検証する
  - フレームスキップ時の範囲判定と手動トリガーの動作を検証する
  - _Requirements: 1.10, 2.6, 2.10_

- [x] 12.2 (P) リレーフローの統合テストを作成する
  - TimecodeEngine にシミュレートされた Timecode を入力し、TimecodeRelay が継続送信とワンショット送信を正しく実行することを検証する
  - 信号喪失時のリレー停止を検証する
  - _Requirements: 4.1, 4.3, 4.9_

- [x] 12.3 (P) プロジェクト保存・読込の統合テストを作成する
  - キュー、ホスト、リレー設定、オフセットを含むプロジェクトデータを保存し、再読込後に全設定が正しく復元されることを検証する
  - _Requirements: 7.1, 7.2_
