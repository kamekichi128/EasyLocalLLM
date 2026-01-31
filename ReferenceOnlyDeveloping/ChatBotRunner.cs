using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.IO;
using System;

/// <summary>
/// ChatBotのサーバーを起動・管理するクラス.
/// シングルトンとしてシーン全体をまたがって管理する.
/// </summary>
[DefaultExecutionOrder(-1)]
public class ChatBotRunner : MonoBehaviour
{
    /// <summary>
    /// サーバーのURL.
    /// </summary>
    public static string API_SERVER_URL = "http://localhost:14334";

    /// <summary>
    /// 生成時のシード.
    /// </summary>
    public static int SEED = 17254;

    /// <summary>
    /// インスタンス.
    /// </summary>
    public static ChatBotRunner Instance { get; private set; }

    /// <summary>
    /// 実行プロセスのパス
    /// </summary>
    public static string EXECUTABLE_PATH = Application.streamingAssetsPath + "/LLM/ollama.exe";

    /// <summary>
    /// OLLAMAモデルのインストール先
    /// </summary>
    public static string OLLAMA_MODELS = "./Models";

    /// <summary>
    /// プロセス.
    /// </summary>
    private Process process = null;


    private void Awake()
    {
        Debug.Log("ChatBotRunner: Awake Called");
        if (Instance == null)
        {
            Instance = this;
            // ChatBotサーバーを起動する
            process = LaunchServer();
            DontDestroyOnLoad(gameObject);
            Debug.Log("ChatBotRunner: Server Launched");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Process LaunchServer()
    {
        // プロセス情報の設定
        ProcessStartInfo startInfo = new()
        {
            FileName = EXECUTABLE_PATH,
            Arguments = "serve",
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,   
            CreateNoWindow = true,
            WorkingDirectory = System.Environment.CurrentDirectory
        };
        startInfo.EnvironmentVariables["OLLAMA_HOST"] = API_SERVER_URL;
        startInfo.EnvironmentVariables["OLLAMA_FLASH_ATTENTION"] = "1";
        startInfo.EnvironmentVariables["OLLAMA_MODELS"] = OLLAMA_MODELS;

        // プロセスの起動
        Process process = new ()
        {
            StartInfo = startInfo,
        };

        process.Start();

        UnityEngine.Debug.Log($"Python script launched with PID: {process.Id}");

        return process;
    }

    private void OnDestroy()
    {
        Debug.Log("ChatBotRunner: OnDestroy Called");
        // ChatBotサーバーを停止する
        if (process != null)
        {
            TerminateServer();
        }
    }

    void TerminateServer()
    {
        Debug.Log("ChatBotRunner: TerminateServer Called");
        if (process != null && !process.HasExited)
        {
            Debug.Log("ChatBotRunner: Process will killed");
            KillProcessTree(process);
            process.Close();
            Debug.Log("ChatBotRunner: Process closed");
        }
        else
        {
            Debug.Log("ChatBotRunner: Process has been closed");
        }
        process = null;
    }

    void KillProcessTree(System.Diagnostics.Process process)
    {
        string taskkill = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
        using (var procKiller = new System.Diagnostics.Process())
        {
            procKiller.StartInfo.FileName = taskkill;
            procKiller.StartInfo.Arguments = string.Format("/PID {0} /T /F", process.Id);
            procKiller.StartInfo.CreateNoWindow = true;
            procKiller.StartInfo.UseShellExecute = false;
            procKiller.Start();
            procKiller.WaitForExit();
        }
    }
}