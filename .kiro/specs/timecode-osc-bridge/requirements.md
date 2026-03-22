# Requirements Document

## Introduction
TimecodeBridgeは、外部から受信したTimecodeを解析し、特定の時間にトリガーされるキューを管理して複数のホストへOSCメッセージを送信するWindows向けアプリケーションである。また、受信中のTimecodeをリアルタイムでOSCメッセージとして他のホストへ中継する機能も備える。ライブイベント、映像制作、舞台演出などの現場で、時間同期に基づく機器制御を実現する。

## Requirements

### Requirement 1: Timecode入力・解析
**Objective:** As a オペレーター, I want 外部ソースからTimecodeを受信・解析したい, so that 正確な時間情報に基づいてキューを実行できる

#### Acceptance Criteria
1. The TimecodeBridge shall LTC（Linear Timecode）をオーディオ入力から受信し解析する機能を提供する
2. The TimecodeBridge shall MTC（MIDI Timecode）をMIDI入力から受信し解析する機能を提供する
3. When Timecodeの受信が開始された時, the TimecodeBridge shall 現在のTimecode値をリアルタイムでUIに表示する
4. The TimecodeBridge shall 24fps、25fps、29.97fps（ドロップフレーム）、30fpsのフレームレートに対応する
5. If Timecodeの信号が途絶した場合, then the TimecodeBridge shall 信号喪失をUIに警告表示し、最後に受信した値を保持する
6. While Timecodeを受信中, the TimecodeBridge shall 受信状態インジケーターを表示する
7. The TimecodeBridge shall 受信したTimecodeに対してオフセット値（±HH:MM:SS:FF）を設定できる
8. When オフセットが設定されている時, the TimecodeBridge shall 受信Timecodeにオフセットを加算した値を「オフセット適用済みTimecode」としてUIに表示する
9. The TimecodeBridge shall 生のTimecodeとオフセット適用済みTimecodeの両方を同時にUIに表示する
10. The TimecodeBridge shall キューのトリガー判定にオフセット適用済みTimecodeを使用する

### Requirement 2: キューリスト管理
**Objective:** As a オペレーター, I want 特定のTimecodeに対応するキューを作成・管理したい, so that 正確なタイミングでOSCメッセージを自動送信できる

#### Acceptance Criteria
1. The TimecodeBridge shall キューの追加・編集・削除・並び替え機能を提供する
2. The TimecodeBridge shall 各キューにトリガー時間（HH:MM:SS:FF形式）を設定できる
3. The TimecodeBridge shall 各キューに送信先ホスト（複数選択可）を設定できる
4. The TimecodeBridge shall 各キューに送信するOSCアドレスパターンとOSC引数を設定できる
5. The TimecodeBridge shall 各キューに名前・メモを設定できる
6. When キューのトリガー時間がオフセット適用済みTimecodeと一致した時, the TimecodeBridge shall 該当キューに設定されたOSCメッセージを選択された送信先ホストへ送信する
7. The TimecodeBridge shall 各キューの有効・無効を切り替えできる
8. While キューが無効に設定されている間, the TimecodeBridge shall そのキューのトリガーをスキップする
9. The TimecodeBridge shall Timecodeを受信していない状態でもキューを手動トリガーできるボタンを各キューに提供する
10. When 手動トリガーボタンが押された時, the TimecodeBridge shall そのキューに設定されたOSCメッセージを即座に送信先ホストへ送信する

### Requirement 3: OSC送信
**Objective:** As a オペレーター, I want 複数のホストに対してOSCメッセージを送信したい, so that 複数の機器やソフトウェアを同時に制御できる

#### Acceptance Criteria
1. The TimecodeBridge shall OSCメッセージをUDP経由で送信する
2. The TimecodeBridge shall 複数の送信先ホスト（IPアドレスとポート番号の組み合わせ）を登録できる
3. The TimecodeBridge shall OSC引数としてint32、float32、string型をサポートする
4. When キューがトリガーされた時, the TimecodeBridge shall そのキューに選択された送信先ホストに対して同時にOSCメッセージを送信する
5. If OSCメッセージの送信に失敗した場合, then the TimecodeBridge shall 送信失敗をログに記録し、UIに通知する
6. The TimecodeBridge shall 各送信先ホストの有効・無効を切り替えできる

### Requirement 4: Timecodeリレー（OSC転送）
**Objective:** As a オペレーター, I want 受信中のTimecodeをOSCメッセージとして他のホストに転送したい, so that Timecodeを受信できない機器にも時間情報を共有できる

#### Acceptance Criteria
1. The TimecodeBridge shall 受信中のオフセット適用済みTimecodeをOSCメッセージとして指定されたホストへリアルタイム送信する「継続送信モード」を提供する
2. The TimecodeBridge shall トリガー操作した瞬間のオフセット適用済みTimecodeのみをOSCメッセージとして送信する「ワンショット送信モード」を提供する
3. When ワンショット送信がトリガーされた時, the TimecodeBridge shall その瞬間のオフセット適用済みTimecodeを1回だけ送信先ホストへ送信する
4. The TimecodeBridge shall リレー送信先ホストを個別に選択できる（送信先ホスト一覧から選別可能）
5. The TimecodeBridge shall リレー用のOSCアドレスパターンをカスタマイズできる
6. The TimecodeBridge shall 継続送信モードの送信間隔（フレーム毎、または指定インターバル）を設定できる
7. When 継続送信モードが有効な時, the TimecodeBridge shall Timecode受信と同期してOSCメッセージを継続送信する
8. The TimecodeBridge shall 継続送信モードの有効・無効を切り替えできる
9. If リレー中にTimecode信号が途絶した場合, then the TimecodeBridge shall リレー送信を停止し、送信先に信号喪失を通知する

### Requirement 5: 送信先ホスト管理
**Objective:** As a オペレーター, I want 送信先ホストを一元管理したい, so that キューやリレーの設定を効率的に行える

#### Acceptance Criteria
1. The TimecodeBridge shall 送信先ホストの一覧を管理する画面を提供する
2. The TimecodeBridge shall 各ホストに名前、IPアドレス、ポート番号を設定できる
3. The TimecodeBridge shall 各ホストの接続テスト（OSC ping送信）機能を提供する
4. When ホストの設定が変更された時, the TimecodeBridge shall そのホストを参照する全てのキュー・リレー設定に変更を反映する
5. The TimecodeBridge shall 各ホストの有効・無効を切り替えできる

### Requirement 6: ユーザーインターフェース
**Objective:** As a オペレーター, I want 直感的で視認性の高いUIで操作したい, so that ライブ現場で迅速かつ正確に操作できる

#### Acceptance Criteria
1. The TimecodeBridge shall Windowsネイティブアプリケーションとして動作する
2. The TimecodeBridge shall 現在のTimecode、キューリスト、送信先ホスト一覧を一画面で確認できるメインウィンドウを提供する
3. The TimecodeBridge shall 次にトリガーされるキューをハイライト表示する
4. When キューがトリガーされた時, the TimecodeBridge shall トリガーされたキューを視覚的にフィードバック表示する
5. The TimecodeBridge shall OSC送受信のログをリアルタイムで表示するログパネルを提供する
6. The TimecodeBridge shall ダークテーマのUIを提供する

### Requirement 7: プロジェクト保存・読み込み
**Objective:** As a オペレーター, I want キューリストやホスト設定をプロジェクトファイルとして保存・読み込みしたい, so that 公演ごとの設定を管理・再利用できる

#### Acceptance Criteria
1. The TimecodeBridge shall キューリスト、送信先ホスト、リレー設定を含むプロジェクトファイルの保存機能を提供する
2. The TimecodeBridge shall プロジェクトファイルの読み込み機能を提供する
3. When アプリケーション終了時に未保存の変更がある場合, the TimecodeBridge shall 保存確認ダイアログを表示する
4. The TimecodeBridge shall 最近使用したプロジェクトファイルの一覧を表示する
