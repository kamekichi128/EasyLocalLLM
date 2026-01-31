# EasyLocalLLM Library テストガイド

新しい Runtime ライブラリのテストスクリプトを実行するためのセットアップとガイドです。

## テストスクリプト概要

3つのテストスクリプトがあります：

### 1. **NonStreamingTest.cs** - 非ストリーミング機能テスト
- シンプルなメッセージ送信
- 温度制御（確定的な回答）
- セッション履歴管理
- エラーハンドリング
- 複数セッション管理

**実行方法**：
1. 新しいシーンを作成
2. GameObject を作成
3. `LibraryTestNonStreaming` スクリプトをアタッチ
4. ゲームを実行

### 2. **StreamingTest.cs** - ストリーミング機能テスト
- シンプルなストリーミング
- 長い回答のストリーミング
- リアルタイム表示シミュレーション
- ストリーミング + セッション履歴

**実行方法**：
1. 新しいシーンを作成
2. GameObject を作成
3. `LibraryTestStreaming` スクリプトをアタッチ
4. ゲームを実行

### 3. **ComparisonTest.cs** - 旧実装との互換性テスト
ReferenceOnlyDeveloping の各シーンコントローラと同等の動作を確認

- **ADVSceneController 非ストリーミング版**
  - 複数メッセージの送信
  - 自動履歴管理
  - リトライロジック検証

- **ADVSceneController ストリーミング版**
  - チャンク処理
  - リアルタイム受信

- **MainSceneController パターン**
  - 言語別システムプロンプト
  - ビジーフラグ管理

- **TitleSceneController パターン**
  - 初期化テスト
  - リトライ機能検証

**実行方法**：
1. 新しいシーンを作成
2. GameObject を作成
3. `LibraryComparisonTest` スクリプトをアタッチ
4. ゲームを実行

---

## 前提条件

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

---

## テスト実行フロー

### テスト 1: NonStreaming - Simple Message

```
Input: "Hello, how are you?"
Expected: AI からの応答を受信
Result: GUI に応答が表示される
```

### テスト 2: NonStreaming - Deterministic Response

```
Input: "What is 2+2?"
Config: Temperature=0.0, Seed=100
Expected: 毎回同じ応答が返される
Result: 複数回実行しても同じ結果
```

### テスト 3: NonStreaming - Session History

```
Flow:
1. "My name is Alice" → AI応答
2. "What is my name?" → "Alice"を覚えている
Result: AI が履歴から情報を取得
```

### テスト 4: NonStreaming - Error Handling

```
Config: ServerUrl = "http://localhost:9999" (存在しないサーバ)
Expected: 
  - ErrorType: ConnectionFailed
  - MaxRetries: 2 回リトライ
  - IsRetryable: true
```

### テスト 5: NonStreaming - Multiple Sessions

```
Flow:
- Session A: "I like programming"
- Session B: "I like cooking"
- Session A: "What is my hobby?" → "programming"を返す
Result: 複数セッションが独立して管理
```

### テスト 6: Streaming - Simple Streaming

```
Input: "Tell me a short joke"
Expected: ドット (.) で進捗を表示
Result: 段階的に応答を受信
```

### テスト 7: Streaming - Long Response

```
Input: "Explain machine learning in detail"
Expected: 複数チャンクに分割
Result: 総チャンク数と最終長を表示
```

### テスト 8: Streaming - Real-time Display

```
Input: "Write a haiku about spring"
Expected: リアルタイムで表示更新
Result: 段階的に詩が表示される
```

### テスト 9: Comparison - ADVScene Pattern

```
Legacy: SendMessageToChatbotAtOnce
New: SendMessageAsync
Result: 同一の動作を確認
```

### テスト 10: Comparison - MainScene Pattern

```
Legacy: inEnglish フラグで言語切り替え
New: ChatRequestOptions.SystemPrompt
Result: 柔軟な設定が可能
```

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
ollama pull neural-chat
```

### ❌ Timeout エラー

**原因**: サーバの応答が遅い、またはネットワーク問題

**対処法**:
1. HttpTimeoutSeconds を増やす
2. ネットワーク接続を確認
3. サーバのリソース使用状況を確認

### ❌ InvalidResponse エラー

**原因**: レスポンス JSON のパース失敗

**対処法**:
1. DebugMode=true で詳細ログを確認
2. Ollama バージョンが対応しているか確認

---

## 新旧実装の対比表

| 機能 | Legacy (ReferenceOnly) | New Library |
|------|------------------------|-------------|
| **メッセージ送信** | `SendMessageToChatbotAtOnce` | `SendMessageAsync` |
| **ストリーミング** | `SendMessageToChatbotStreaming` | `SendMessageStreamingAsync` |
| **履歴管理** | 手動（List<ChatMessage>） | 自動（ChatHistoryManager） |
| **リトライ** | 各メソッド内で実装 | HttpRequestHelper で統一 |
| **エラー情報** | string のみ | ChatError enum で詳細 |
| **設定** | ハードコード | OllamaConfig で柔軟 |
| **サーバ管理** | ChatBotRunner | OllamaServerManager |
| **セッション** | ID ベース | ChatSession オブジェクト |

---

## テスト完了チェックリスト

- [ ] NonStreaming - Simple Message ✅
- [ ] NonStreaming - Deterministic Response ✅
- [ ] NonStreaming - Session History ✅
- [ ] NonStreaming - Error Handling ✅
- [ ] NonStreaming - Multiple Sessions ✅
- [ ] Streaming - Simple ✅
- [ ] Streaming - Long Response ✅
- [ ] Streaming - Real-time Display ✅
- [ ] Comparison - ADVScene Non-Streaming ✅
- [ ] Comparison - ADVScene Streaming ✅
- [ ] Comparison - MainScene ✅
- [ ] Comparison - TitleScene ✅

すべてのテストが ✅ になれば、ライブラリは本番環境で使用可能です。

---

## 次のステップ

テスト完了後：

1. **ReferenceOnlyDeveloping の実装を新ライブラリに置き換え**
   - ADVSceneController → LibraryTestNonStreaming + LibraryTestStreaming に統合
   - MainSceneController → 新ライブラリで簡潔化
   - TitleSceneController → 初期化処理を新ライブラリで統一

2. **Asset Store 用ドキュメント作成**
   - Getting Started ガイド
   - API リファレンス
   - サンプルプロジェクト

3. **追加機能の検討**
   - llama.cpp クライアント対応
   - async/await 対応
   - 永続化機能
