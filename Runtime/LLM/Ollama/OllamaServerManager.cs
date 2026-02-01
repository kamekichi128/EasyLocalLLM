using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Ollama サーバのライフサイクルを管理するクラス
    /// ChatBotRunner の機能をライブラリに統合
    /// </summary>
    public class OllamaServerManager : MonoBehaviour
    {
        private static OllamaServerManager _instance;
        public static OllamaServerManager Instance => _instance;

        private OllamaConfig _config;
        private Process _process;
        private bool _isRunning = false;

        /// <summary>
        /// サーバの起動状態を取得
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// OllamaServerManager を初期化（設定指定）
        /// </summary>
        public static void Initialize(OllamaConfig config)
        {
            if (_instance != null)
            {
                UnityEngine.Debug.LogWarning("OllamaServerManager is already initialized");
                return;
            }

            GameObject managerGO = new GameObject("OllamaServerManager");
            _instance = managerGO.AddComponent<OllamaServerManager>();
            _instance._config = config;
            DontDestroyOnLoad(managerGO);

            if (config.AutoStartServer)
            {
                _instance.StartServer();
            }
        }

        /// <summary>
        /// デフォルト設定で初期化
        /// </summary>
        public static void Initialize()
        {
            Initialize(new OllamaConfig());
        }

        /// <summary>
        /// サーバを起動
        /// </summary>
        public void StartServer()
        {
            if (_isRunning)
            {
                UnityEngine.Debug.LogWarning("Ollama server is already running");
                return;
            }

            if (_config == null)
            {
                UnityEngine.Debug.LogError("OllamaServerManager has not been initialized. Call Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(_config.ExecutablePath))
            {
                UnityEngine.Debug.LogWarning("ExecutablePath is not set. Server auto-start is skipped.");
                _isRunning = false;
                return;
            }

            if (!File.Exists(_config.ExecutablePath))
            {
                UnityEngine.Debug.LogError($"Ollama executable not found: {_config.ExecutablePath}");
                _isRunning = false;
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = _config.ExecutablePath,
                    Arguments = "serve",
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.Environment.CurrentDirectory
                };

                // 環境変数を設定
                if (Uri.TryCreate(_config.ServerUrl, UriKind.Absolute, out var serverUri))
                {
                    string hostPort = serverUri.IsDefaultPort
                        ? serverUri.Host
                        : $"{serverUri.Host}:{serverUri.Port}";
                    startInfo.EnvironmentVariables["OLLAMA_HOST"] = hostPort;
                }
                else
                {
                    startInfo.EnvironmentVariables["OLLAMA_HOST"] = _config.ServerUrl;
                }
                startInfo.EnvironmentVariables["OLLAMA_FLASH_ATTENTION"] = "1";
                startInfo.EnvironmentVariables["OLLAMA_MODELS"] = _config.ModelsDirectory;

                _process = new Process { StartInfo = startInfo };
                _process.Start();
                _isRunning = true;

                if (_config.DebugMode)
                {
                    UnityEngine.Debug.Log($"Ollama server launched with PID: {_process.Id}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to start Ollama server: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// サーバを停止
        /// </summary>
        public void StopServer()
        {
            if (!_isRunning || _process == null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    KillProcessTree(_process);
                    _process.Close();

                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.Log("Ollama server stopped");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error stopping Ollama server: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _process = null;
            }
        }

        /// <summary>
        /// プロセスツリーを強制終了
        /// </summary>
        private void KillProcessTree(Process process)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    string taskkill = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "taskkill.exe");

                    if (!File.Exists(taskkill))
                    {
                        // taskkill が見つからない場合、直接プロセスを終了
                        process.Kill();
                        return;
                    }

                    using (var procKiller = new Process())
                    {
                        procKiller.StartInfo.FileName = taskkill;
                        procKiller.StartInfo.Arguments = $"/PID {process.Id} /T /F";
                        procKiller.StartInfo.CreateNoWindow = true;
                        procKiller.StartInfo.UseShellExecute = false;
                        procKiller.Start();
                        procKiller.WaitForExit();
                    }
                }
                else
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error killing process tree: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }
    }
}
