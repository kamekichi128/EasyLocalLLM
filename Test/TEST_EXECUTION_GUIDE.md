# テスト実行ガイド

## 概要

EasyLocalLLMライブラリには、モックを使用した単体テストが含まれています。
Ollamaサーバーを起動せずにテストを実行できます。

## テストファイル構成

```
Test/
├── MockChatLLMClient.cs         # IChatLLMClientのモック実装
├── NonStreamingTests.cs         # 非ストリーミングAPI用の単体テスト (7テスト)
├── StreamingTests.cs            # ストリーミングAPI用の単体テスト (7テスト)
└── TestRunner.cs                # 簡易テストランナー
```

## 実行方法

### 方法1: Unity Test Runner（推奨）

1. Unity エディタで `Window > General > Test Runner` を開く
2. `PlayMode` タブを選択
3. `Run All` をクリックしてすべてのテストを実行

**利点:**
- 標準的なUnityテストフレームワーク
- 詳細なテスト結果が表示される
- 個別のテスト実行が可能

### 方法2: TestRunner スクリプト（簡易版）

Unity Test Runnerが使えない場合、簡易的なテストランナーを使用できます。

**手順:**

1. 空のシーンを作成
2. 空のGameObjectを作成（名前: `TestRunner`）
3. `TestRunner.cs` スクリプトをアタッチ
4. Playモードで実行
5. ゲーム画面のボタンをクリック、またはInspectorで `Run All Tests` を右クリック → `Context Menu` から実行

**使用可能なボタン:**
- `Run All Tests` - すべてのテストを実行
- `Run NonStreaming Tests Only` - 非ストリーミングテストのみ
- `Run Streaming Tests Only` - ストリーミングテストのみ

**結果の確認:**
- Console ウィンドウでテスト結果を確認
- 成功: `✓ PASSED` (緑色)
- 失敗: `✗ FAILED` (赤色)

## テスト内容

### NonStreamingTests.cs (7テスト)

| # | テスト名 | 検証内容 |
|---|---------|---------|
| 1 | Test_SimpleMessage_ReturnsResponse | 基本的なメッセージ送信 |
| 2 | Test_DeterministicResponse_WithTemperatureZero | 確定的な回答（Temperature=0） |
| 3 | Test_SessionHistory_RemembersContext | セッション履歴の保持 |
| 4 | Test_ErrorHandling_ReturnsError | エラーハンドリング |
| 5 | Test_MultipleSessions_MaintainSeparateHistory | 複数セッションの独立管理 |
| 6 | Test_TaskAPI_ReturnsResponse | Task版APIの動作 |
| 7 | Test_CustomMockResponse_ReturnsCustomContent | カスタムレスポンスの設定 |

### StreamingTests.cs (7テスト)

| # | テスト名 | 検証内容 |
|---|---------|---------|
| 1 | Test_SimpleStreaming_ReceivesMultipleChunks | 基本的なストリーミング |
| 2 | Test_LongResponseStreaming_ReceivesProgressiveUpdates | 長文レスポンスの段階的受信 |
| 3 | Test_RealTimeDisplay_AccumulatesContent | リアルタイム表示のシミュレーション |
| 4 | Test_StreamingWithHistory_RemembersContext | 履歴を含むストリーミング |
| 5 | Test_StreamingError_ReturnsError | エラーハンドリング |
| 6 | Test_TaskStreamingAPI_ReportsProgress | Task版ストリーミングAPIの進捗報告 |
| 7 | Test_StreamingInterruption_HandlesGracefully | ストリーミング中断の処理 |

## モックの特徴

`MockChatLLMClient` は以下の機能をサポート:

- ✅ セッション履歴の管理
- ✅ 名前や好みの記憶（簡易的な文脈理解）
- ✅ エラーシミュレーション
- ✅ カスタムレスポンスの設定
- ✅ ストリーミングのシミュレーション
- ✅ レスポンス遅延の調整

## トラブルシューティング

### テストが実行されない

- `CoroutineRunner.Instance` が正しく動作しているか確認
- Unity エディタがPlayモードであることを確認

### テストが失敗する

- Consoleウィンドウでエラーメッセージを確認
- `MockChatLLMClient` の設定を確認（特にレスポンス遅延）

### パフォーマンスが遅い

- `MockChatLLMClient.SetResponseDelay()` で遅延時間を短縮
- 個別のテストのみを実行

## 次のステップ

単体テストが成功したら:
1. `Samples/QuickStartTest.cs` で実際のOllamaサーバーとの統合テスト
2. カスタムシーンでライブラリを使用
3. 本番環境への統合
