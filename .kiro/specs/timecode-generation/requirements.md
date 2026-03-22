# Requirements Document

## Introduction
TimecodeBridgeは現在、外部ソースからLTCタイムコードを受信・デコードする機能を備えている。本機能追加では、TimecodeBridge自身がタイムコードを内部生成し、LTCオーディオ信号として出力する「タイムコードジェネレーター」機能を実装する。これにより、外部タイムコードソースが不要な環境や、TimecodeBridgeをマスタークロックとして使用するワークフローに対応する。生成されたタイムコードは既存のキュートリガーやOSCリレー機能とシームレスに連携する。

## Requirements

### Requirement 1: タイムコード内部生成
**Objective:** As a オペレーター, I want TimecodeBridge内部でタイムコードを生成したい, so that 外部タイムコードソースがなくてもキューのトリガーやOSCリレーを利用できる

#### Acceptance Criteria
1. The TimecodeBridge shall 内部クロックに基づいてフレーム精度のタイムコードを生成する機能を提供する
2. The TimecodeBridge shall 生成タイムコードのフレームレートを24fps、25fps、29.97fps（ドロップフレーム）、30fpsから選択できる
3. The TimecodeBridge shall 生成タイムコードの開始時間をHH:MM:SS:FF形式で設定できる
4. When 生成が開始された時, the TimecodeBridge shall 設定された開始時間からフレームレートに同期してタイムコードをカウントアップする
5. The TimecodeBridge shall タイムコード生成の開始・停止・リセット操作を提供する
6. When リセットが実行された時, the TimecodeBridge shall 現在のタイムコード値を設定された開始時間に戻す
7. While タイムコードを生成中, the TimecodeBridge shall 生成されたタイムコード値をリアルタイムでUIに表示する
8. The TimecodeBridge shall 生成されたタイムコードを既存のTimecodeEngineと同じタイムコードソースとして扱い、キュートリガーおよびOSCリレー機能と連携する

### Requirement 2: タイムコードソース切り替え
**Objective:** As a オペレーター, I want 外部受信と内部生成のタイムコードソースを切り替えたい, so that 状況に応じて最適なタイムコードソースを使用できる

#### Acceptance Criteria
1. The TimecodeBridge shall タイムコードソースとして「LTC受信」と「内部生成」を選択できるUIを提供する
2. When タイムコードソースが切り替えられた時, the TimecodeBridge shall 新しいソースのタイムコードをキュートリガーおよびOSCリレーに即座に反映する
3. While 内部生成モードが選択されている間, the TimecodeBridge shall オーディオ入力の選択UIを無効化する
4. While LTC受信モードが選択されている間, the TimecodeBridge shall タイムコード生成の制御UI（開始・停止・リセット）を無効化する
5. The TimecodeBridge shall 現在選択中のタイムコードソースをUIに明示的に表示する

### Requirement 3: LTCオーディオ出力
**Objective:** As a オペレーター, I want 生成したタイムコードをLTCオーディオ信号として出力したい, so that LTC入力を持つ外部機器にタイムコードを供給できる

#### Acceptance Criteria
1. The TimecodeBridge shall 生成されたタイムコードをLTC（Linear Timecode）オーディオ信号としてエンコードする機能を提供する
2. The TimecodeBridge shall LTC出力先のオーディオデバイスを選択できる
3. The TimecodeBridge shall LTC出力の音量レベルを調整できる
4. When タイムコード生成が開始された時, the TimecodeBridge shall 選択されたオーディオデバイスからLTCオーディオ信号を出力する
5. When タイムコード生成が停止された時, the TimecodeBridge shall LTCオーディオ出力を停止する
6. While LTC出力中, the TimecodeBridge shall 出力状態インジケーターをUIに表示する

### Requirement 4: 生成設定の永続化
**Objective:** As a オペレーター, I want タイムコード生成の設定をプロジェクトファイルに保存したい, so that 公演ごとの生成設定を再利用できる

#### Acceptance Criteria
1. The TimecodeBridge shall タイムコード生成設定（フレームレート、開始時間、出力デバイス、音量レベル）をプロジェクトファイルに保存する
2. When プロジェクトファイルが読み込まれた時, the TimecodeBridge shall 保存された生成設定を復元する
3. The TimecodeBridge shall タイムコードソースの選択状態をプロジェクトファイルに保存する
