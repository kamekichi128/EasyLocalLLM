# EasyLocalLLM テストシーン作成手順

Unity エディタでテストシーンを準備するための手順です。

## シーン 1: NonStreaming Test Scene

### 準備手順

1. **新しいシーンを作成**
   - File → New Scene
   - 名前: `NonStreamingTestScene.unity`
   - Assets/EasyLocalLLM/Samples/Scenes/ に保存

2. **GameObject を作成**
   - Hierarchy で右クリック
   - Create Empty
   - 名前: `NonStreamingTestManager`

3. **スクリプトをアタッチ**
   - Inspector で Add Component
   - Script → `LibraryTestNonStreaming` を検索
   - アタッチ

4. **実行**
   - Play ボタンをクリック
   - GUI が表示されます
   - 各テストボタンをクリック

### GUI ボタン

| ボタン | 説明 |
|--------|------|
| Test 1: Simple Message | 基本的なメッセージ送受信 |
| Test 2: Temperature 0 | 確定的な応答テスト |
| Test 3: Session History | 複数メッセージでの履歴管理 |
| Test 4: Error Handling | エラーハンドリング（意図的に失敗させる） |
| Test 5: Multiple Sessions | 複数セッション管理 |
| Clear Output | 出力をクリア |

---

## シーン 2: Streaming Test Scene

### 準備手順

1. **新しいシーンを作成**
   - File → New Scene
   - 名前: `StreamingTestScene.unity`
   - Assets/EasyLocalLLM/Samples/Scenes/ に保存

2. **GameObject を作成**
   - Hierarchy で右クリック
   - Create Empty
   - 名前: `StreamingTestManager`

3. **スクリプトをアタッチ**
   - Inspector で Add Component
   - Script → `LibraryTestStreaming` を検索
   - アタッチ

4. **実行**
   - Play ボタンをクリック
   - 各テストボタンをクリック

### GUI ボタン

| ボタン | 説明 |
|--------|------|
| Test 1: Simple Streaming | 基本的なストリーミング |
| Test 2: Long Response Streaming | 長い応答のストリーミング |
| Test 3: Real-time Display | リアルタイム表示シミュレーション |
| Test 4: Streaming with History | セッション履歴を含むストリーミング |
| Clear Output | 出力をクリア |

---

## シーン 3: Comparison Test Scene

### 準備手順

1. **新しいシーンを作成**
   - File → New Scene
   - 名前: `ComparisonTestScene.unity`
   - Assets/EasyLocalLLM/Samples/Scenes/ に保存

2. **GameObject を作成**
   - Hierarchy で右クリック
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

---

## ベストプラクティス

### GUI テスト時

1. **テストを一つずつ実行**
   - 複数同時実行は避ける
   - 各テスト間に 1-2 秒の待機

2. **出力を記録**
   - テスト完了後、出力をコピー
   - スクリーンショットを保存

3. **エラー対応**
   - ConnectionFailed → Ollama サーバを確認
   - TimeOut → サーバのリソースを確認
   - InvalidResponse → DebugMode=true で詳細確認

### パフォーマンス測定

出力に含まれるログから：

```
Chunk 1: 150ms
Chunk 2: 200ms
...
Total Time: 3.5s
```

---

## トラブルシューティング

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

**確認**:
```csharp
void OnGUI()
{
    GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
    // ...
}
```

### テストが開始しない（ボタンをクリックしても反応なし）

**原因**: `_testInProgress` が true のままになっている

**対処法**:
1. 前のテストが完全に終了するまで待機
2. シーンを再ロード（Ctrl+R）
3. Play を再開始

---

## テスト完了後の次のステップ

1. **すべてのテストがパスしたか確認**
   - チェックリスト（TEST_GUIDE.md 参照）をすべて埋める

2. **パフォーマンスデータを記録**
   - レスポンス時間
   - チャンク数
   - メモリ使用量

3. **ReferenceOnlyDeveloping との置き換え**
   - ADVSceneController.cs を削除
   - 新ライブラリを使用する実装に変更

4. **Asset Store 用ドキュメント整理**
   - サンプルシーンを Assets/Samples に移動
   - README を最終化

---

## 参考リンク

- [TEST_GUIDE.md](./TEST_GUIDE.md) - 詳細なテスト手順
- [Runtime/LLM/README.md](../Runtime/LLM/README.md) - API ドキュメント
