using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Class for managing Ollama server lifecycle
    /// Integrates ChatBotRunner functionality into the library
    /// </summary>
    public class OllamaServerManager : MonoBehaviour
    {
        private static OllamaServerManager _instance;
        public static OllamaServerManager Instance => _instance;

        private OllamaConfig _config;
        private Process _process;
        private bool _isRunning = false;
        private Action<bool> _initializationCallback;
        private Thread _stdoutReaderThread;
        private Thread _stderrReaderThread;
        private volatile bool _logReaderStopRequested;
        private readonly Queue<QueuedLogEntry> _logQueue = new();
        private readonly object _logQueueLock = new();

        private struct QueuedLogEntry
        {
            public LogType Type;
            public string Message;
        }

        /// <summary>
        /// Get server running status
        /// </summary>
        public bool IsRunning => _isRunning;

        private void Update()
        {
            FlushQueuedLogs();
        }

        /// <summary>
        /// Initialize OllamaServerManager (with config + callback)
        /// </summary>
        /// <param name="config">Ollama configuration</param>
        /// <param name="initializationCallback">Callback on initialization complete (true on success)</param>
        public static void Initialize(OllamaConfig config, Action<bool> initializationCallback = null)
        {
            if (_instance != null)
            {
                UnityEngine.Debug.LogWarning("OllamaServerManager is already initialized");
                initializationCallback?.Invoke(false);
                return;
            }

            GameObject managerGO = new("OllamaServerManager");
            _instance = managerGO.AddComponent<OllamaServerManager>();
            _instance._config = config;
            _instance._initializationCallback = initializationCallback;
            DontDestroyOnLoad(managerGO);

            if (config.AutoStartServer)
            {
                _instance.StartServer();
            }
            else
            {
                initializationCallback?.Invoke(true);
            }
        }

        /// <summary>
        /// Initialize with default configuration
        /// </summary>
        public static void Initialize()
        {
            Initialize(new OllamaConfig());
        }

        /// <summary>
        /// Start server
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
                _initializationCallback?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(_config.ExecutablePath))
            {
                UnityEngine.Debug.LogWarning("ExecutablePath is not set. Server auto-start is skipped.");
                _isRunning = false;
                _initializationCallback?.Invoke(false);
                return;
            }

            if (!File.Exists(_config.ExecutablePath))
            {
                UnityEngine.Debug.LogError($"Ollama executable not found: {_config.ExecutablePath}");
                _isRunning = false;
                _initializationCallback?.Invoke(false);
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
                    RedirectStandardOutput = _config.DebugMode,
                    RedirectStandardError = _config.DebugMode,
                    CreateNoWindow = true,
                    WorkingDirectory = System.Environment.CurrentDirectory
                };

                // Set environment variables
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

                if (_config.DebugMode)
                {
                    StartLogReaderThreads();
                }
                _isRunning = true;

                if (_config.DebugMode)
                {
                    UnityEngine.Debug.Log($"Ollama server launched with PID: {_process.Id}");
                }

                if (_config.EnableHealthCheck)
                {
                    StartCoroutine(HealthCheckCoroutine());
                }
                else
                {
                    _initializationCallback?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to start Ollama server: {ex.Message}");
                _isRunning = false;
                _initializationCallback?.Invoke(false);
            }
        }

        /// <summary>
        /// Stop server
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
        /// Force terminate process tree
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
                        // taskkill not found, fallback to simple kill
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

        /// <summary>
        /// Server health check (simple request to confirm operation)
        /// </summary>
        private IEnumerator HealthCheckCoroutine()
        {
            int maxAttempts = 30;
            float delaySeconds = 1.0f;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                attempt++;
                yield return new WaitForSeconds(delaySeconds);

                // Get /api/tags
                using (UnityWebRequest request = new UnityWebRequest(_config.ServerUrl + "/api/tags", "GET"))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 5;

                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log("[Ollama] Health check passed - server is ready");
                        }
                        _initializationCallback?.Invoke(true);
                        yield break;
                    }
                    else
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Health check attempt {attempt}/{maxAttempts} failed: {request.error}");
                        }
                    }
                }
            }

            UnityEngine.Debug.LogError($"[Ollama] Health check failed after {maxAttempts} attempts");
            _initializationCallback?.Invoke(false);
        }

        private void StartLogReaderThreads()
        {
            if (_process == null)
            {
                return;
            }

            _logReaderStopRequested = false;

            _stdoutReaderThread = new Thread(() =>
                ReadStreamLoop(_process.StandardOutput, LogType.Log, "stdout"))
            {
                IsBackground = true,
                Name = "OllamaStdoutReader"
            };

            _stderrReaderThread = new Thread(() =>
                ReadStreamLoop(_process.StandardError, LogType.Warning, "stderr"))
            {
                IsBackground = true,
                Name = "OllamaStderrReader"
            };

            _stdoutReaderThread.Start();
            _stderrReaderThread.Start();
        }

        private void StopLogReaderThreads()
        {
            _logReaderStopRequested = true;

            if (_stdoutReaderThread != null && _stdoutReaderThread.IsAlive)
            {
                _stdoutReaderThread.Join(300);
            }

            if (_stderrReaderThread != null && _stderrReaderThread.IsAlive)
            {
                _stderrReaderThread.Join(300);
            }

            _stdoutReaderThread = null;
            _stderrReaderThread = null;
        }

        private void ReadStreamLoop(StreamReader reader, LogType logType, string streamName)
        {
            StringBuilder lineBuffer = new();
            char[] chunk = new char[512];
            DateTime lastFlushAt = DateTime.UtcNow;
            TimeSpan partialFlushInterval = TimeSpan.FromMilliseconds(250);

            try
            {
                while (!_logReaderStopRequested)
                {
                    int read = reader.Read(chunk, 0, chunk.Length);

                    if (read <= 0)
                    {
                        break;
                    }

                    for (int i = 0; i < read; i++)
                    {
                        char current = chunk[i];

                        if (current == '\r')
                        {
                            continue;
                        }

                        if (current == '\n')
                        {
                            if (lineBuffer.Length > 0)
                            {
                                EnqueueLog(logType, $"[Ollama {streamName}] {lineBuffer}");
                                lineBuffer.Clear();
                            }

                            lastFlushAt = DateTime.UtcNow;
                            continue;
                        }

                        lineBuffer.Append(current);
                    }

                    if (lineBuffer.Length > 0 && DateTime.UtcNow - lastFlushAt >= partialFlushInterval)
                    {
                        EnqueueLog(logType, $"[Ollama {streamName}] {lineBuffer}");
                        lineBuffer.Clear();
                        lastFlushAt = DateTime.UtcNow;
                    }
                }

                if (lineBuffer.Length > 0)
                {
                    EnqueueLog(logType, $"[Ollama {streamName}] {lineBuffer}");
                }
            }
            catch (Exception ex)
            {
                if (!_logReaderStopRequested)
                {
                    EnqueueLog(LogType.Error, $"[Ollama {streamName}] Reader error: {ex.Message}");
                }
            }
        }

        private void EnqueueLog(LogType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (_logQueueLock)
            {
                _logQueue.Enqueue(new QueuedLogEntry
                {
                    Type = type,
                    Message = message
                });
            }
        }

        private void FlushQueuedLogs()
        {
            while (true)
            {
                QueuedLogEntry entry;

                lock (_logQueueLock)
                {
                    if (_logQueue.Count == 0)
                    {
                        break;
                    }

                    entry = _logQueue.Dequeue();
                }

                switch (entry.Type)
                {
                    case LogType.Warning:
                        UnityEngine.Debug.LogWarning(entry.Message);
                        break;
                    case LogType.Error:
                    case LogType.Assert:
                    case LogType.Exception:
                        UnityEngine.Debug.LogError(entry.Message);
                        break;
                    default:
                        UnityEngine.Debug.Log(entry.Message);
                        break;
                }
            }
        }


        private void OnDestroy()
        {
            StopLogReaderThreads();
            StopServer();
            FlushQueuedLogs();
        }
    }
}
