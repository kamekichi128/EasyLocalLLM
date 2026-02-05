# EasyLocalLLM テストシーン作成手順

Unity エディタで統合テストを実行するためのシーン設定手順です。

## 概要

このドキュメントでは **QuickStartTest** を使用した統合テストシーンの作成方法を説明します。

より詳細なテスト（単体テスト）を実行したい場合は、[../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照してください。

---

## QuickStart Test Scene（統合テスト）

実際のOllamaサーバーとの通信を確認する統合テストです。

### 前提条件

- ✅ Ollama サーバが起動している（`ollama serve`）
- ✅ モデルがインストールされている（`ollama pull mistral`）

### 準備手順

1. **新しいシーンを作成**
   - File → New Scene
   - 名前: `QuickStartTestScene.unity`
   - Assets/EasyLocalLLM/Samples/ に保存

2. **GameObject を作成**
   - Hierarchy で右クリック
   - Create Empty
   - 名前: `QuickStartManager`

3. **スクリプトをアタッチ**
   - Inspector で Add Component
   - Script → `QuickStartTest` を検索
   - アタッチ

4. **実行**
   - Play ボタンをクリック
   - Console ウィンドウでログを確認

### テスト内容

QuickStartTest は以下の4つのステップを実行します：

| ステップ | 説明 | 期待される出力 |
|---------|------|--------------|
| 1 | OllamaClient初期化 | `✓ Client initialized` |
| 2 | シンプルなメッセージ送信 | `✓ Response received: ...` |
| 3 | セッション履歴でのフォローアップ | `✓ Follow-up response: ...` |
| 4 | ストリーミング機能テスト | `✓ Streaming completed!` |

### 成功時の出力例

```
===詳細なテストについて

より詳細な単体テスト（Ollama サーバ不要）を実行したい場合：

- **テスト場所**: [../Test/](../Test/)
- **実行方法**: [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照
- **テスト内容**:
  - NonStreamingTests.cs（7テスト）
  - StreamingTests.cs（7テスト）
  - すべてモック実装で動作
   - Create Empty
   - 名前: `ComparisonTestManager`

3. **スクリプトをアタッチ**
   - Inspector で Add Component
   - Script → `LibraryComparisonTest` を検索
   - アタッチ

4. **実行**
   - Play ボタンをクリック
   - 各テストボタンをクリック

### GUI ボタン

| ボタン | 説明 |
|--------|------|
| Test: ADVSceneController Pattern (Non-Streaming) | 旧実装との比較（非ストリーミング） |
| Test: ADVSceneController Pattern (Streaming) | 旧実装との比較（ストリーミング） |
| Test: MainSceneController Pattern | MainScene の実装パターン比較 |
| Test: TitleScene Initialization Pattern | TitleScene の初期化パターン比較 |
| Clear Output | 出力をクリア |

---

## テスト実行時の注意点

### ✅ 推奨設定

**Scene Setup**:
```
Camera: Standard Main Camera
Canvas: (オプション) UI 表示用
```

**Project Settings**:
- API Compatibility Level: .NET Framework
- (または) .NET Standard 2.1

### ⚙️ デバッグコンソール

テスト中は Unity Console でログが出力されます：

```
=== EasyLocalLLM NonStreaming Test Start ===
[Ollama] Sending request (attempt 1/3)
[Ollama] URL: http://localhost:11434/api/chat
[Ollama] Response received: {...}
```

DebugMode=true の場合、詳細なログが表示されます。

---

## テスト結果の記録

### 成功例

```
[Test 1] Simple Message Test
Sending: "Hello, how are you?"
---
Response: I'm doing well, thank you for asking! How can I help you today?
---
[Test 1] Complete
```

### エラー例

```
[Test 4] Error Handling Test
Attempting to connect to wrong server...
---
ErrorType: ConnectionFailed
Message: Failed to connect to server
HttpStatus: 0
IsRetryable: True
---
[Test 4] Complete
```
Quick Start ===
[Step 1] Initializing OllamaClient...
[Ollama] Sending request (attempt 1/3)
[Ollama] URL: http://localhost:11434/api/chat
[Ollama] Response received: {...}
```

DebugMode=true の場合、詳細なログが表示されます。

---

## テスト結果の記録

### 成功例

```
[Step 2] Sending simple message...
✓ Response received: I'm doing well, thank you for asking! How can I help you today?
```

### エラー例

```
[Step 2] Sending simple message...
✗ Error: ConnectionFailed - Failed to connect to server
  HttpStatus: 0
### シーンが読み込めない

```
error CS0246: The type or namespace name 'LibraryTestNonStreaming' could not be found
```

**対処法**:
1. スクリプトが Assets/EasyLocalLLM/Samples/ に存在するか確認
2. Assets フォルダを右クリック → Reimport
3. スクリプトの namespace を確認（最初の行に `using EasyLocalLLM.LLM;` があるか）

### GUI が表示されない

**原因**: スクリプトが OnGUI メソッドを持っていない

**確認テスト実行時

1. **Ollama サーバを先に起動**
   ```bash
   ollama serve
   ```

2. **モデルを確認**
   ```bash
   ollama list
   ```

3. **テスト実行**
   - Play ボタンをクリック
   - Console ウィンドウを開いておく

4. **出力を確認**
   - 成功: "=== All Quick Tests Passed ===" が表示
   - 失敗: エラーメッセージを確認

### パフォーマンス測定

QuickStartTest の出力から以下が確認できます：

```
Total chunks: 15           # ストリーミングのチャンク数
Response length: 245 chars # 最終レスポンスの長さ
```

---

## トラブルシューティング

### ❌ "Failed to connect to server" エラー

**原因**: Ollama サーバが起動していない

**対処法**:
```bash
ollama serve
```

### ❌ "Model not found" エラー

**原因**: 指定されたモデルがダウンロードされていない

**対処法**:
```bash
ollama pull mistral
```

### ❌ Timeout エラー

**原因**: サーバの応答が遅い

**対処法**:
1. HttpTimeoutSeconds を増やす
   ```csharp
   config.HttpTimeoutSeconds = 60; // デフォルト: 30
   ```
2. サーバのリソース使用状況を確認
3. ネットワーク接続を確認

### ❌ Console ログが表示されない

**原因**: Scripting Backend が不正な設定

**対処法**:
1. Window → General → Console を開く
2. Debug.Log が有効か確認
3. Scripting Backend = Mono であることを確認

---

## テスト完了後の次のステップ

1. **詳細な単体テストを実行**（推奨）
   - [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) を参照
   - 全14テスト（NonStreaming 7 + Streaming 7）

2. **パフォーマンスデータを記録**
   - レスポンス時間
   - チャンク数
   - メモリ使用量

3. **カスタムシーンでライブラリを使用**
   - 実際のプロジェクト内で OllamaClient を使用

4. **本番環境への統合**
   - ライブラリが完全に動作確認できたら本番環境へ

---

## 参考リンク

- [TEST_GUIDE.md](./TEST_GUIDE.md) - QuickStartTest の詳細ガイド
- [../Test/TEST_EXECUTION_GUIDE.md](../Test/TEST_EXECUTION_GUIDE.md) - 詳細な単体テスト実行方法
- [../Runtime/README.md](../Runtime/README.md) - API ドキュメント
- [QuickStart.md](../QuickStart.md) - ライブラリの基本使用方法