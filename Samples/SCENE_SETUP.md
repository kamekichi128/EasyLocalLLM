# EasyLocalLLM テストシーン作成手順

Unity エディタで統合テストを実行するためのシーン設定手順です。

## 概要

このドキュメントでは **QuickStartTest** を使用した統合テストシーンの作成方法を説明します。

---

## QuickStart Test Scene（統合テスト）

実際のOllamaサーバーとの通信を確認する統合テストです。

### 前提条件

- ✅ Ollama サーバが起動している（`ollama serve`）
- ✅ モデルがインストールされている（`ollama pull mistral`）

### 準備手順

1. **新しいシーンを作成**
   - Assets/EasyLocalLLM/Samples/Scenes/で右クリック -> Create -> Scene -> Scene
   - 名前: `QuickStartTestScene.unity`
   - 作成したシーンをダブルクリックして開く

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

1. **カスタムシーンでライブラリを使用**
   - 実際のプロジェクト内で OllamaClient を使用

2. **本番環境への統合**
   - ライブラリが完全に動作確認できたら本番環境へ

---

## 参考リンク

- [../Runtime/README.md](../Runtime/README.md) - API ドキュメント
- [QuickStart.md](../QuickStart.md) - ライブラリの基本使用方法