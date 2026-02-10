# EasyLocalLLM QuickStartシーン作成手順

Unity エディタでQuickStartを実行するためのシーン設定手順です。

## 概要

このドキュメントでは **QuickStart** を使用したシーンの作成方法を説明します。

---

## QuickStart Scene

実際のOllamaサーバーと通信を行い、LLMを動かすテストです。

### 前提条件

- ✅ Ollama サーバが起動している（`ollama serve`）
- ✅ モデルがインストールされている（`ollama pull mistral`）

### 準備手順

1. **新しいシーンを作成**
   - Assets/EasyLocalLLM/Samples/Scenes/で右クリック -> Create -> Scene -> Scene
   - 名前: `QuickStartScene.unity`
   - 作成したシーンをダブルクリックして開く

2. **GameObject を作成**
   - Hierarchy で右クリック
   - Create Empty
   - 名前: `QuickStartManager`

3. **スクリプトをアタッチ**
   - Inspector で Add Component
   - Script → `QuickStart` を検索
   - アタッチ

4. **実行**
   - Play ボタンをクリック
   - Console ウィンドウでログを確認

### テスト内容

QuickStart は以下の4つのステップを実行します：

| ステップ | 説明 | 期待される出力 |
|---------|------|--------------|
| 1 | OllamaClient初期化 | `✓ Client initialized` |
| 2 | シンプルなメッセージ送信 | `✓ Response received: ...` |
| 3 | セッション履歴でのフォローアップ | `✓ Follow-up response: ...` |
| 4 | ストリーミング機能テスト | `✓ Streaming completed!` |
| 5 | ツール使用機能テスト | `✓ Tool call passed!` |


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

1. **実際の使い方を学ぶ**
   - SimpleChat / LateralThinkingQuiz を参照して実際のゲームでの使い方を学びましょう
   - シーンはUnity 6.2（6000.2.6f2）で構築済みです

2. **カスタムシーンでライブラリを使用**
   - この手順書を参考にカスタムシーンにEasyLocalLLMをインポートして使いましょう

---

## 参考リンク

- [Documentation/API_Reference.md](../Documentation/API_Reference.md) - API ドキュメント