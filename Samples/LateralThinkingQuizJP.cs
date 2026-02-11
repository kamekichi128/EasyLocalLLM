using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.Ollama;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 水平思考クイズゲームのサンプル
/// 4種類のAIを組み合わせた、簡単なチャットアプリケーションの例。
/// 1) 水平思考クイズゲームのお題を出すAI（お題AI）
/// 2) 出されたお題に対してYes/Noで答えるAI（Yes/No AI）
/// 3) Yes/Noの回答をもとに回答を試みるAI（対戦相手AI）
/// 4) 回答が正解かどうかを判定するAI（判定AI）
/// 
/// ゲーム開始ボタンをクリックすると、お題AIが、水平思考クイズゲームのお題を、「お題」と「真相」のペアで生成する
/// お題AIの生成した「お題」と「真相」はYes/No AIと、判定AIに渡される
/// 「お題」は対戦相手AIに渡され、対戦相手AIは回答を試みる。ただし、もっと情報が必要な場合は、Yes/No AIに質問を行う
/// 判定AIは、プレイヤーと対戦相手AIの回答が正解かどうかを判定し、正解ならcorrect、不正解ならincorrectを返す
/// 正解が出た時点でゲーム終了となり、真相が表示される
/// 
/// 想定しているUIの部品は以下の通り
/// 
/// ・お題生成ボタン（PuzzleGenerateButton）
/// ・ギブアップボタン（GiveupButton）
/// ・お題表示ラベル（PuzzleLabel）
/// ・プレイヤーの質問入力欄（PlayerQuestionInput）
/// ・プレイヤーの質問送信ボタン（PlayerQuestionSendButton）
/// ・プレイヤーの回答入力欄（PlayerAnswerInput）
/// ・プレイヤーの回答送信ボタン（PlayerAnswerSendButton）
/// ・チャット履歴表示欄（ChatHistory）
/// ・真相表示ラベル（TruthLabel）
/// </summary>
public class LateralThinkingQuizJP : MonoBehaviour
{
    public UIDocument UIDocument;

    private IChatLLMClient client;

    // ゲーム状態
    private string currentPuzzle = "";
    private string currentTruth = "";
    private List<string> chatHistory = new();
    private bool isGameActive = false;
    private Coroutine opponentCoroutine;

    // AI セッションID
    private const string JudgeSession = "Judge";

    void Start()
    {
        Debug.Log("=== EasyLocalLLM 水平思考クイズゲームサンプル ===");
        InitializeEasyLocalLLMClient();
    }

    void OnDisable()
    {
        RemoveUIEvent();
    }

    private void InitializeEasyLocalLLMClient()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            DefaultModelName = "hoangquan456/qwen3-nothink:8b",
            AutoStartServer = true,
            DebugMode = true,
        };
        OllamaServerManager.Initialize(config, OnOllamaServerInitialized);
        client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("✓ Client initialized");
    }

    private void OnOllamaServerInitialized(bool successed)
    {
        if (successed)
        {
            Debug.Log("✓ Ollama server initialized successfully.");
            EnableUI();
        }
        else
        {
            Debug.LogError("✗ Failed to initialize Ollama server.");
        }
    }

    private void EnableUI()
    {
        var root = UIDocument.rootVisualElement;
        var puzzleGenerateButton = root.Q<Button>("PuzzleGenerateButton");
        var playerQuestionSendButton = root.Q<Button>("PlayerQuestionSendButton");
        var playerAnswerSendButton = root.Q<Button>("PlayerAnswerSendButton");
        var giveupButton = root.Q<Button>("GiveupButton");
        var truethLabel = root.Q<Label>("TruthLabel");

        puzzleGenerateButton.clicked += OnPuzzleGenerateClicked;
        playerQuestionSendButton.clicked += OnPlayerQuestionSendClicked;
        playerAnswerSendButton.clicked += OnPlayerAnswerSendClicked;
        giveupButton.clicked += OnGiveupClicked;

        playerQuestionSendButton.SetEnabled(false);
        playerAnswerSendButton.SetEnabled(false);
        giveupButton.SetEnabled(false);
        truethLabel.style.display = DisplayStyle.None;

        OnPuzzleGenerateClicked();
    }

    private void OnGiveupClicked()
    {
        // ゲームを放棄し、真相を表示して終了
        GameOver(false);
    }

    private void RemoveUIEvent()
    {
        var root = UIDocument.rootVisualElement;
        var puzzleGenerateButton = root.Q<Button>("PuzzleGenerateButton");
        var playerQuestionSendButton = root.Q<Button>("PlayerQuestionSendButton");
        var playerAnswerSendButton = root.Q<Button>("PlayerAnswerSendButton");
        var giveupButton = root.Q<Button>("GiveupButton");

        puzzleGenerateButton.clicked -= OnPuzzleGenerateClicked;
        playerQuestionSendButton.clicked -= OnPlayerQuestionSendClicked;
        playerAnswerSendButton.clicked -= OnPlayerAnswerSendClicked;
        giveupButton.clicked -= OnGiveupClicked;
    }

    private void OnPuzzleGenerateClicked()
    {
        // セッションをクリア
        client.ClearAllMessages();

        // UIを初期化
        var root = UIDocument.rootVisualElement;
        var puzzleGenerateButton = root.Q<Button>("PuzzleGenerateButton");
        var puzzleLabel = root.Q<Label>("PuzzleLabel");
        var truethLabel = root.Q<Label>("TruthLabel");
        var chatHistory = root.Q<Label>("ChatHistory");
        var giveupButton = root.Q<Button>("GiveupButton");

        puzzleGenerateButton.SetEnabled(false);
        puzzleLabel.text = "<お題生成中...>";
        truethLabel.text = "";
        truethLabel.style.display = DisplayStyle.None;
        isGameActive = false;
        this.chatHistory.Clear();


        // お題AIが「お題」と「真相」を生成
        StartCoroutine(client.SendMessageAsync(
            "水平思考クイズゲームを遊びたいです。\n"
            + "水平思考クイズゲームとは、出題者が参加者と遊ぶ会話型の謎解きゲームです。\n"
            + "出題者は「お題」と「真相」を準備し、ゲーム開始時に「お題」を提示します。\n"
            + "この「お題」の文章は、それだけでは答えの分からない不可解な出来事の情景を説明した文章で、締めくくりは「なぜだろう？」等の疑問形の一言で終わります。\n"
            + "以下に例を3つ示します。\n\n"
            + "お題 : タクシー運転手が、一方通行の道を逆方向に走っていた。パトロール中の警察官に見られてしまったが、怒られなかった。なぜだろう？\n"
            + "真相 : タクシー運転手は車に乗っておらず、一方通行の道を徒歩で逆方向に進んでいただけだった。車が一方通行でも、歩きならば関係ない。\n\n"
            + "お題 : ある女性は天気のよい日に外に出たら逮捕された。なぜだろう？\n"
            + "真相 : ある女性は脱獄犯だったから。脱獄犯が刑務所の外に出たが、天気のよい日だったため見つかりやすく逮捕されてしまった。\n\n"
            + "お題 : 鍵を持っていない男は、家の前で女を待っていた。その直後、女が帰ってきて鍵を開けると、男はその場を立ち去ってしまった。いったいなぜ？\n"
            + "真相 : 配達員の男は、インターホンを押して反応を待っていたが、女が帰ってきたので荷物を渡して帰った。女は配達時間を帰宅時間と同じくらいに設定していたが、配達員のほうがわずかに早く到着してしまった。"
            + "あなたは例として挙げたお題以外の、新しいお題と真相を考えてください。お題、真相は可能な限り簡潔に記載してください。超常現象や超能力、サイエンスフィクション（SF）などは避けてください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error generating puzzle: {error.Message}");
                    puzzleGenerateButton.SetEnabled(true);
                    return;
                }

                if (!response.IsFinal)
                    return;

                // レスポンスをパース
                ExtractPuzzleAndTruth(response.Content, out var puzzle, out var truth);
                currentPuzzle = puzzle;
                currentTruth = truth;

                Debug.Log($"Puzzle: {puzzle}");
                Debug.Log($"Truth: {truth}");

                puzzleLabel.text = puzzle;

                this.chatHistory.Clear();
                isGameActive = true;
                AddChatMessage($"[Puzzle Master]\n{puzzle}");

                // ゲーム開始
                var playerQuestionSendButton = root.Q<Button>("PlayerQuestionSendButton");
                var playerAnswerSendButton = root.Q<Button>("PlayerAnswerSendButton");
                var giveupButton = root.Q<Button>("GiveupButton");
                playerQuestionSendButton.SetEnabled(true);
                playerAnswerSendButton.SetEnabled(true);
                giveupButton.SetEnabled(true);

                // 対戦相手AIの自動質問・回答ループを開始
                opponentCoroutine = StartCoroutine(OpponentAILoop());
            },
            new ChatRequestOptions
            {
                SystemPrompt = "あなたは非常に有能で、論理的かつ想像力に溢れたゲームの問題出題者です。日本語で応答してください。",
                FormatSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        puzzle = new { type = "string", description = "お題" },
                        truth = new { type = "string", description = "真相", maxLength = 500 },
                    },
                    required = new[] { "puzzle", "truth" }
                },
                WaitIfBusy = true,
                Priority = 75
            }
        ));
    }

    private void OnPlayerQuestionSendClicked()
    {
        var root = UIDocument.rootVisualElement;
        var questionInput = root.Q<TextField>("PlayerQuestionInput");
        string question = questionInput.value;

        if (string.IsNullOrWhiteSpace(question))
            return;

        questionInput.value = "";

        // プレイヤーの質問をYes/No AIに送信
        AddChatMessage($"[Player Question]\n{question}");

        StartCoroutine(client.SendMessageAsync(
            $"お題：{currentPuzzle}\n真相：{currentTruth}\n\n質問：{question}\n\nYes、No、または分からないで短く答えてください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    return;
                }

                if (!response.IsFinal)
                    return;

                AddChatMessage($"[Yes/No Answerer]\n{response.Content}");
            },
            new ChatRequestOptions
            {
                SystemPrompt = "あなたはあるクイズの非常に論理的で有能なYes/No回答者です。" +
                    "与えられた真相に基づいて、真相に関する質問にYesまたはNo、または分からないで答えてください。理由は延べないでください。",
                Priority = 100,
                WaitIfBusy = true
            }
        ));
    }

    private void AskOpponentAI()
    {
        // 対戦相手AIが自動で質問を試みる
        // チャット履歴をコンテキストに含め、単発のセッションで推測させる
        string chatContext = string.Join("\n", chatHistory);

        StartCoroutine(client.SendMessageAsync(
            $"これまでのやり取り：\n{chatContext}\n\n" +
            "あなたは今、あるお題を読んで、そのお題の裏側にある真相を答えるゲームをしています。これまでの質問と回答から、このお題の真相を推測してください。情報が足りないと考えた場合は、真相を突き止めるのに有用なこれまでに出ていない、必ず**Yes/Noで答えられる**質問を投げかけてください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    return;
                }

                if (!response.IsFinal)
                    return;

                try
                {
                    var opponentResponse = JsonUtility.FromJson<OpponentResponseData>(response.Content);

                    if (opponentResponse.type == "question")
                    {
                        // 質問の場合、Yes/No AIに問い合わせる
                        AddChatMessage($"[Opponent]\n質問：{opponentResponse.content}");
                        OnYesNoAnswerOpponentQuestion(opponentResponse.content);
                    }
                    else if (opponentResponse.type == "answer")
                    {
                        // 答えの場合、判定AIで判定
                        AddChatMessage($"[Opponent]\n答え：{opponentResponse.content}");
                        JudgeOpponentAnswer(opponentResponse.content);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse opponent response: {e.Message}\nResponse: {response.Content}");
                }
            },
            new ChatRequestOptions
            {
                SystemPrompt = "あなたはクイズゲームの非常に論理的で有能な対戦相手です。" +
                    "与えられたお題と過去の質問応答から論理的に真相の推測を進めてください。日本語で応答してください。",
                Priority = 50,
                WaitIfBusy = true,
                FormatSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        type = new { type = "string", @enum = new[] { "question", "answer" }, description = "質問するならquestion、答えを返すならanswer" },
                        content = new { type = "string", description = "質問の内容または答えの内容" }
                    },
                    required = new[] { "type", "content" }
                }
            }
        ));
    }

    private void OnYesNoAnswerOpponentQuestion(string question)
    {
        // 対戦相手からの質問に対応する単発のセッション
        StartCoroutine(client.SendMessageAsync(
            $"お題：{currentPuzzle}\n真相：{currentTruth}\n\n質問：{question}\n\nYes、No、または分からないで短く答えてください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    return;
                }

                AddChatMessage($"[Yes/No Answerer (Opponent Query)]\n{response.Content}");
            },
            new ChatRequestOptions
            {
                SystemPrompt = "あなたはあるクイズの非常に論理的で有能なYes/No回答者です。" +
                    "与えられた真相に基づいて、真相に関する質問にYesまたはNo、または分からないで答えてください。理由は延べないでください。",
                Priority = 100,
                WaitIfBusy = true
            }
        ));
    }


    private void OnPlayerAnswerSendClicked()
    {
        var root = UIDocument.rootVisualElement;
        var answerInput = root.Q<TextField>("PlayerAnswerInput");
        string answer = answerInput.value;

        if (string.IsNullOrWhiteSpace(answer))
            return;

        answerInput.value = "";

        AddChatMessage($"[Player Answer]\n{answer}");

        // 判定AIで正解判定
        JudgePlayerAnswer(answer);
    }

    private void JudgePlayerAnswer(string answer)
    {
        StartCoroutine(client.SendMessageAsync(
            $"クイズのお題は『{currentPuzzle}』、真相は『{currentTruth}』です。\n" +
            $"プレイヤーの回答『{answer}』が正解かどうか判定してください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    return;
                }

                try
                {
                    var judgement = JsonUtility.FromJson<JudgementData>(response.Content);
                    string resultText = judgement.result == "correct" ? "正解" : "不正解";
                    AddChatMessage($"[Judge]\n{resultText}");
                    Debug.Log($"Opponent Answer Judged: {resultText} : {judgement.reason}");

                    if (judgement.result == "correct")
                    {
                        GameOver(true);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse judge response: {e.Message}\nResponse: {response.Content}");
                }
            },
            new ChatRequestOptions
            {
                SessionId = JudgeSession,
                SystemPrompt = "あなたはクイズの非常に論理的で有能な正解/不正解の判定者です。日本語で応答してください。",
                Priority = 100,
                WaitIfBusy = true,
                FormatSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        result = new { type = "string", @enum = new[] { "correct", "incorrect" }, description = "判定結果。正解ならcorrect、不正解ならincorrect" },
                        reason = new { type = "string", description = "判定理由" }
                    },
                    required = new[] { "result", "reason" }
                }
            }
        ));
    }

    private void JudgeOpponentAnswer(string opponentAnswer)
    {
        StartCoroutine(client.SendMessageAsync(
            $"クイズゲームのお題は『{currentPuzzle}』、真相は『{currentTruth}』です。\n" +
            $"対戦相手AIの回答『{opponentAnswer}』が正解かどうか判定してください。",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    return;
                }

                if (!response.IsFinal)
                    return;

                try
                {
                    var judgement = JsonUtility.FromJson<JudgementData>(response.Content);
                    string resultText = judgement.result == "correct" ? "正解" : "不正解";
                    AddChatMessage($"[Judge]\n{resultText}");
                    Debug.Log($"Opponent Answer Judged: {resultText} : {judgement.reason}");

                    if (judgement.result == "correct")
                    {
                        GameOver(false);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse judge response: {e.Message}\nResponse: {response.Content}");
                }
            },
            new ChatRequestOptions
            {
                SessionId = JudgeSession,
                SystemPrompt = "あなたはクイズゲームの非常に論理的で有能な正解/不正解の判定者です。",
                Priority = 100,
                WaitIfBusy = true,
                FormatSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        result = new { type = "string", @enum = new[] { "correct", "incorrect" }, description = "判定結果。正解ならcorrect、不正解ならincorrect" },
                        reason = new { type = "string", description = "判定理由" }
                    },
                    required = new[] { "result", "reason" }
                }
            }
        ));
    }

    private void GameOver(bool playerWon)
    {
        // ゲーム状態を終了し、Coroutineを停止
        isGameActive = false;
        if (opponentCoroutine != null)
        {
            StopCoroutine(opponentCoroutine);
            opponentCoroutine = null;
        }

        var root = UIDocument.rootVisualElement;
        var truethLabel = root.Q<Label>("TruthLabel");
        var playerQuestionSendButton = root.Q<Button>("PlayerQuestionSendButton");
        var playerAnswerSendButton = root.Q<Button>("PlayerAnswerSendButton");
        var giveupButton = root.Q<Button>("GiveupButton");
        var puzzleGenerateButton = root.Q<Button>("PuzzleGenerateButton");

        playerQuestionSendButton.SetEnabled(false);
        playerAnswerSendButton.SetEnabled(false);
        giveupButton.SetEnabled(false);
        puzzleGenerateButton.SetEnabled(true);
        truethLabel.style.display = DisplayStyle.Flex;

        truethLabel.text = $"【真相】\n{currentTruth}\n\n{(playerWon ? "プレイヤーの勝利！" : "対戦相手AIの勝利！")}";
    }

    private System.Collections.IEnumerator OpponentAILoop()
    {
        // 最初は10秒待ってから開始
        yield return new UnityEngine.WaitForSeconds(10f);

        while (isGameActive)
        {
            Debug.Log("対戦相手AIのターン開始");
            AskOpponentAI();

            // 10秒待機
            yield return new UnityEngine.WaitForSeconds(10f);
        }
    }

    private void AddChatMessage(string message)
    {
        if (isGameActive)
        {
            chatHistory.Add(message);
            UpdateChatHistoryUI();
        }
    }

    private void UpdateChatHistoryUI()
    {
        var root = UIDocument.rootVisualElement;
        var chatHistoryLabel = root.Q<Label>("ChatHistory");
        var invertedHistory = new List<string>(chatHistory);
        invertedHistory.Reverse();
        chatHistoryLabel.text = string.Join("\n\n", invertedHistory);
    }

    private void ExtractPuzzleAndTruth(string response, out string puzzle, out string truth)
    {
        puzzle = "";
        truth = "";

        try
        {
            // JSON形式で返される: {"puzzle": "...", "truth": "..."}
            var data = JsonUtility.FromJson<PuzzleData>(response);
            puzzle = data.puzzle;
            truth = data.truth;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to parse puzzle response: {e.Message}\nResponse: {response}");
        }
    }

    [System.Serializable]
    private class PuzzleData
    {
        public string puzzle;
        public string truth;
    }

    [System.Serializable]
    private class JudgementData
    {
        public string result;
        public string reason;
    }

    [System.Serializable]
    private class OpponentResponseData
    {
        public string type;
        public string content;
    }
}
