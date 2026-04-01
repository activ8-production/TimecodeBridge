# Requirements Document

## Introduction
TimecodeBridgeアプリケーションのコード品質・保守性・テスト容易性を向上させるためのリファクタリング仕様。現在のアーキテクチャは堅実なMVVM基盤を持つが、一部のクラスに責務の集中、イベント購読の未解除、ダイアログ処理のコードビハインド依存などの課題がある。本リファクタリングでは、既存機能の外部動作を一切変えずに内部構造を改善する。

## Requirements

### Requirement 1: TimecodeViewModelの責務分離
**Objective:** As a 開発者, I want TimecodeViewModelの責務を適切に分離したい, so that 各コンポーネントの変更影響範囲を限定し、個別にテスト可能にできる

#### Acceptance Criteria
1. The TimecodeViewModel shall オーディオデバイス列挙・キャッシュのロジックをViewModelの外部に委譲する
2. The TimecodeViewModel shall ジェネレーター状態管理のロジックをViewModelの外部に委譲する
3. The TimecodeViewModel shall LTCキャプチャ状態管理のロジックをViewModelの外部に委譲する
4. The TimecodeViewModel shall UIバインディングとコマンドのみを保持し、ビジネスロジックを含まない
5. When 責務分離後にアプリケーションを起動した場合, the TimecodeViewModel shall リファクタリング前と同一の外部動作を維持する

### Requirement 2: TimecodeEngineの内部構造整理
**Objective:** As a 開発者, I want TimecodeEngineの内部処理を論理単位で整理したい, so that 各処理の理解・修正・テストが容易になる

#### Acceptance Criteria
1. The TimecodeEngine shall LTCキャプチャに関する処理を独立したクラスに抽出する
2. The TimecodeEngine shall ジェネレーターに関する処理を独立したクラスに抽出する
3. The TimecodeEngine shall フリーラン（信号喪失時の自動補完）に関する処理を独立したクラスに抽出する
4. The TimecodeEngine shall オーケストレーター（調整役）として抽出した各クラスを統合する
5. When 内部構造を整理した後にタイムコード処理を実行した場合, the TimecodeEngine shall リファクタリング前と同一のタイムコード出力を維持する

### Requirement 3: ProjectServiceの関心事分離
**Objective:** As a 開発者, I want ProjectServiceからアプリケーション設定管理を分離したい, so that プロジェクトファイルI/Oと設定永続化を独立して変更・テスト可能にできる

#### Acceptance Criteria
1. The ProjectService shall プロジェクトファイルの読み書きのみを責務とする
2. The システム shall 最近使用したプロジェクトの管理を専用のサービスとして提供する
3. The システム shall 背景画像設定などのアプリケーション設定の永続化を専用のサービスとして提供する
4. When 分離後にプロジェクトの保存・読み込みを行った場合, the ProjectService shall リファクタリング前と同一のファイル形式・動作を維持する

### Requirement 4: ViewModelのイベント購読管理
**Objective:** As a 開発者, I want ViewModelのイベント購読を適切に管理したい, so that メモリリークの潜在的リスクを排除できる

#### Acceptance Criteria
1. The 各ViewModel shall IDisposableを実装し、購読したイベントをDispose時に解除する
2. When ViewModelが破棄される場合, the ViewModel shall すべてのイベントハンドラの登録を解除する
3. The DIコンテナ shall ViewModelのライフサイクルに応じた適切なDispose呼び出しを保証する

### Requirement 5: ダイアログ処理のサービス化
**Objective:** As a 開発者, I want ダイアログ表示をサービスインターフェースとして抽象化したい, so that ダイアログ関連のロジックをテスト可能にし、コードビハインドへの依存を軽減できる

#### Acceptance Criteria
1. The システム shall キュー編集ダイアログの表示を専用のサービスインターフェースとして提供する
2. The システム shall ファイルダイアログ（開く・保存）の表示を専用のサービスインターフェースとして提供する
3. The CueListViewModel shall ダイアログ表示にFunc委譲ではなくサービスインターフェースを使用する
4. When ダイアログサービスを通じてダイアログを表示した場合, the システム shall リファクタリング前と同一のユーザー体験を維持する

### Requirement 6: エラーハンドリングの改善
**Objective:** As a 開発者, I want 空のcatchブロックを適切なエラー処理に置き換えたい, so that 問題発生時の原因調査が容易になる

#### Acceptance Criteria
1. The システム shall 空のcatchブロック（bare catch）を排除し、最低限のログ出力を行う
2. The システム shall 例外をキャッチする際に、可能な限り具体的な例外型を指定する
3. If 例外が発生した場合, the システム shall グレースフルデグラデーション（機能低下での継続動作）の方針を維持しつつ、問題を記録する

### Requirement 7: 既存テストの維持と拡充
**Objective:** As a 開発者, I want リファクタリング後も既存テストがすべて通過し、新規抽出クラスにもテストを追加したい, so that リファクタリングによるデグレッションを防止できる

#### Acceptance Criteria
1. When リファクタリングを完了した場合, the テストスイート shall 既存のすべてのテストが通過する
2. The テストスイート shall 新たに抽出したサービスクラスに対するユニットテストを含む
3. The テストスイート shall ViewModelの主要なコマンド・プロパティ変更に対するテストを含む
