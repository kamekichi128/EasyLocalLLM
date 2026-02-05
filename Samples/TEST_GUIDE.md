# EasyLocalLLM Library テストガイド

新しい Runtime ライブラリのテストスクリプトを実行するためのセットアップとガイドです。

## テスト構成

テストは2つのレベルに分かれています：

### 1. **Samplesディレクトリ内のテスト** - 統合テスト
- **QuickStartTest.cs** - 実際のOllamaサーバーとの統合テスト
  - クライアント初期化
  - メッセージ送受信
  - セッション履歴
  - ストリーミング機能
  - **所要時間**: ~30秒
  - **前提条件**: Ollama サーバが起動している

### 2. **Test ディレクトリ内のテスト** - 単体テスト（推奨）
詳細は [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照してください。

以下の3つのテストが利用可能です：

**Mock ベース（Ollama 不要）**:
- **NonStreamingTests.cs** - 7つの非ストリーミング単体テスト
- **StreamingTests.cs** - 7つのストリーミング単体テスト
- **MockChatLLMClient.cs** - テスト用モック実装

**テストランナー**:
- **TestRunner.cs** - Unity Test Runnerが利用できない場合の簡易ランナー

---

## QuickStartTest.cs の実行

### 前提条件

### 必須
1. **Ollama サーバが起動していること**
   ```
   ollama serve
   ```

2. **モデルがダウンロード済みであること**
   ```
   ollama pull mistral
   ```

3. **サーバURL が正しく設定されていること**
   - デフォルト: `http://localhost:11434`

### オプション
- DebugMode を true に設定してログを確認

###QuickStartTest の実行フロー

### ステップ 1: クライアント初期化

```
Expected: OllamaClient が正常に初期化される
Result: "✓ Client initialized" がログに表示
```

### ステップ 2: シンプルなメッセージ送信

```
Input: "Hello! What is your name?"
Expected: AI からの応答を受信
Result: "✓ Response received: ..." がログに表示
```

### ステップ 3: セッション履歴でのフォローアップメッセージ

```
Input: "Can you repeat your name again?"
Expected: 前のメッセージとのコンテキストを維持
Result: "✓ Follow-up response: ..." がログに表示
```

### ステップ 4: ストリーミングテスト

```
Input: "Tell me a very brief fact about space"
Expected: 段階的にレスポンスを受信
Result: "✓ Streaming completed!" がログに表示
```

---

## 詳細な単体テストの実行

**Ollama サーバを起動したくない場合、またはより詳細なテストを実行したい場合**:

詳細は [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照してください。

- 7つの非ストリーミング単体テスト
- 7つのストリーミング単体テスト
- すべてモックを使用（Ollama 不要）ected: 複数チャンクに分割
Result: 総チャンク数と最終長を表示
```

### テスト 8: Streaming - Real-time Display

```
Input: "Write a haiku about spring"
Expected: リアルタイムで表示更新
Result: 段階的に詩が表示される
```

---

---

## よくあるエラーと対処法

### ❌ ConnectionFailed エラー

**原因**: Ollama サーバが起動していない

**対処法**:
```bash
ollama serve
```

### ❌ ModelNotFound エラー

**原因**: 指定されたモデルがダウンロードされていない

**対処法**:
```bash
ollama pull mistral
```

### ❌ Timeout エラー

**原因**: サーバの応答が遅い、またはネットワーク問題

**対処法**:
1. HttpTimeoutSeconds を増やす
2. ネットワーク接続を確認
3. サーバのリソース使用状況を確認

---

## テスト完了チェックリスト

### QuickStartTest
- [ ] 実行完了
- [ ] 4つのステップログが表示される
- [ ] エラーがない
- [ ] 完了ログ "=== All Quick Tests Passed ===" が表示される

### 詳細なテストについて
詳細な単体テストの実行方法は [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照してください。

---

## 次のステップ

QuickStartTest が成功したら、以下のいずれかを実行してください：

1. **詳細な単体テストを実行**（推奨）
   - Ollama サーバを起動せずにテスト
   - 7 + 7 = 14個の単体テスト
   - 詳細: [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md)

2. **カスタムシーンでライブラリを使用**
   - 実際のプロジェクト内でOllamaClientを使用
   - Runtime/README.md を参照

3. **本番環境への統合**
   - ライブラリが完全に動作確認できたら本番環境へ
