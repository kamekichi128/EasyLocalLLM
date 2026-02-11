# EasyLocalLLM

Unity でローカル LLM（Ollama）を簡単に使えるようにするライブラリです。わずか数行のコードで、オフラインで動作する AI チャットボットや、ゲーム内 NPC との自然な会話を実装できます。

## ✨ 主な機能

- 🚀 **簡単セットアップ**: 最小限のコードで動作（3行で開始可能）
- 💬 **ストリーミング対応**: リアルタイムで回答を段階的に受け取れる
- 🔧 **柔軟な設定管理**: `OllamaConfig` で詳細なカスタマイズが可能
- 🔄 **自動リトライ**: ネットワークエラー時の指数バックオフ対応
- 📝 **セッション管理**: 複数の会話を同時に管理可能
- 🎭 **システムプロンプト**: セッションごとに異なる役割やキャラクターを設定
- 🛠️ **Tools 対応**: Function Calling でゲーム機能を LLM から呼び出せる
- 🔐 **セキュリティ**: チャット履歴の暗号化と永続化に対応

## 🎯 クイックスタート

わずか数行で動作します：

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class QuickStart : MonoBehaviour
{
    void Start()
    {
        var client = LLMClientFactory.CreateOllamaClient();
        StartCoroutine(client.SendMessageAsync(
            "こんにちは！",
            response => Debug.Log($"AI: {response.Content}")
        ));
    }
}
```

**前提条件**: Ollama サーバが `localhost:11434` で起動済み、`mistral` モデルがインストール済み

Ollamaサーバ自体のセットアップ、あるいはOllamaサーバを含めたゲームの構築については [Ollama サーバの自動管理](Documentation_JP/API_Reference.md#44-ollama-サーバの自動管理) を参照してください。

## 💻 動作環境

- **Unity バージョン**: Unity 2021.3 以上推奨
- **対応 OS**: Windows 10/11
- **必要なもの**: Ollama サーバ（セットアップ方法は完全ドキュメント参照）
- **GPU**: 推奨（CPU でも動作しますが、応答速度が遅くなります）

## 📖 ドキュメント

詳細な使用方法、API リファレンス、サンプルコード、トラブルシューティングは以下をご覧ください：

- **[Documentation_JP/API_Reference.md](Documentation_JP/API_Reference.md)** - 技術ドキュメント
- **[Samples/QuickStart.md](Samples/QuickStart.md)** - 初心者向けガイド

### 主なトピック

- [基本的な初期化](Documentation_JP/API_Reference.md#41-基本的な初期化)
- [ストリーミング送信](Documentation_JP/API_Reference.md#43-ストリーミング送信段階的に回答を受け取る)
- [Ollama サーバの自動管理](Documentation_JP/API_Reference.md#44-ollama-サーバの自動管理)
- [セッション管理](Documentation_JP/API_Reference.md#45-セッション管理)
- [システムプロンプト](Documentation_JP/API_Reference.md#46-システムプロンプト)
- [ツール（Function Calling）](Documentation_JP/API_Reference.md#411-ツールfunction-calling)
- [エラーハンドリング](Documentation_JP/API_Reference.md#410-リトライとエラーハンドリング)

## 📦 サンプル

`Samples/` フォルダに以下のサンプルシーンが含まれています：

- **SimpleChat** - 基本的なチャット UI の実装例
- **LateralThinkingQuiz** - ウミガメのスープ（水平思考クイズゲーム）の実装例
- **QuickStartTest** - 最小限の動作確認

## 🔧 制限事項

- Unity 専用（UnityWebRequest に依存）
- Windows のみ対応
- Task 版 API も内部的にはコルーチンで動作します

## 📄 ライセンス

本ライブラリは MIT ライセンスの下で提供されます。

```
MIT License

Copyright (c) 2026 EasyLocalLLM

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 🤝 サポート

- **バグ報告、リクエスト**: バグ報告や機能リクエストは Github Issuesまで。返答・対応を保証するものではありません。ご留意ください。
- **ドキュメント**: [Documentation_JP/API_Reference.md](Documentation_JP/API_Reference.md) に詳細情報
- **サンプルコード**: `Samples/` フォルダをご覧ください

---

**EasyLocalLLM で、Unity ゲームに AI の力を！** 🎮✨
