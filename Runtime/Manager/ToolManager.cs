using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// ツールの登録・管理・実行を行うマネージャー
    /// </summary>
    public class ToolManager
    {
        private readonly Dictionary<string, Core.ToolDefinition> _tools = new();
        private readonly bool _debugMode;

        public ToolManager(bool debugMode = false)
        {
            _debugMode = debugMode;
        }

        /// <summary>
        /// ツールを登録（スキーマ自動生成）
        /// </summary>
        public void RegisterTool(string name, string description, Delegate callback)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Tool name cannot be null or empty", nameof(name));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            // JSON Schema を自動生成
            var schema = Core.ToolSchemaGenerator.GenerateSchema(callback);
            var paramInfos = Core.ToolSchemaGenerator.GetParameterInfos(callback);
            var returnType = Core.ToolSchemaGenerator.GetReturnType(callback);

            var toolDef = new Core.ToolDefinition
            {
                Name = name,
                Description = description ?? name,
                InputSchema = schema,
                Callback = callback,
                ParameterInfos = paramInfos,
                ReturnType = returnType
            };

            _tools[name] = toolDef;

            if (_debugMode)
            {
                Debug.Log($"[ToolManager] Registered tool: {name}");
                Debug.Log($"[ToolManager] Schema: {schema.ToString(Formatting.Indented)}");
            }
        }

        /// <summary>
        /// ツールを登録（手動スキーマ指定）
        /// </summary>
        public void RegisterTool(string name, string description, object inputSchema, Delegate callback)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Tool name cannot be null or empty", nameof(name));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            JObject schema;
            if (inputSchema is JObject jObj)
            {
                schema = jObj;
            }
            else if (inputSchema is string jsonStr)
            {
                schema = JObject.Parse(jsonStr);
            }
            else
            {
                // 匿名型を JSON に変換
                var json = JsonConvert.SerializeObject(inputSchema);
                schema = JObject.Parse(json);
            }

            var paramInfos = Core.ToolSchemaGenerator.GetParameterInfos(callback);
            var returnType = Core.ToolSchemaGenerator.GetReturnType(callback);

            var toolDef = new Core.ToolDefinition
            {
                Name = name,
                Description = description ?? name,
                InputSchema = schema,
                Callback = callback,
                ParameterInfos = paramInfos,
                ReturnType = returnType
            };

            _tools[name] = toolDef;

            if (_debugMode)
            {
                Debug.Log($"[ToolManager] Registered tool: {name} (manual schema)");
            }
        }

        /// <summary>
        /// ツールを削除
        /// </summary>
        public bool UnregisterTool(string name)
        {
            var result = _tools.Remove(name);

            if (_debugMode && result)
            {
                Debug.Log($"[ToolManager] Unregistered tool: {name}");
            }

            return result;
        }

        /// <summary>
        /// すべてのツールを削除
        /// </summary>
        public void RemoveAllTools()
        {
            var count = _tools.Count;
            _tools.Clear();

            if (_debugMode)
            {
                Debug.Log($"[ToolManager] Removed all tools ({count} tools)");
            }
        }

        /// <summary>
        /// 登録済みツール一覧を取得
        /// </summary>
        public List<Core.ToolDefinition> GetAllTools()
        {
            return _tools.Values.ToList();
        }

        /// <summary>
        /// ツールが存在するか確認
        /// </summary>
        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        /// <summary>
        /// ツールを実行（型変換自動対応）
        /// </summary>
        public string ExecuteTool(string toolName, string argumentsJson)
        {
            if (!_tools.TryGetValue(toolName, out var toolDef))
            {
                throw new InvalidOperationException($"Tool '{toolName}' not found");
            }

            try
            {
                if (_debugMode)
                {
                    Debug.Log($"[ToolManager] Executing tool: {toolName}");
                    Debug.Log($"[ToolManager] Arguments: {argumentsJson}");
                }

                // JSON Arguments をパース
                var argsJson = JObject.Parse(argumentsJson);

                // パラメータを型変換
                var parameters = ConvertParameters(argsJson, toolDef.ParameterInfos);

                // コールバックを実行
                var result = toolDef.Callback.DynamicInvoke(parameters);

                // 戻り値を文字列に変換
                var resultString = ConvertResultToString(result, toolDef.ReturnType);

                if (_debugMode)
                {
                    Debug.Log($"[ToolManager] Tool result: {resultString}");
                }

                return resultString;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error executing tool '{toolName}': {ex.Message}";

                if (_debugMode)
                {
                    Debug.LogError($"[ToolManager] {errorMsg}");
                    Debug.LogException(ex);
                }

                return errorMsg;
            }
        }

        /// <summary>
        /// JSON パラメータを C# オブジェクトに変換
        /// </summary>
        private object[] ConvertParameters(JObject argsJson, List<System.Reflection.ParameterInfo> paramInfos)
        {
            var parameters = new object[paramInfos.Count];

            for (int i = 0; i < paramInfos.Count; i++)
            {
                var paramInfo = paramInfos[i];
                var paramName = paramInfo.Name;
                var paramType = paramInfo.ParameterType;

                // JSON から値を取得
                if (argsJson.TryGetValue(paramName, out var token))
                {
                    parameters[i] = ConvertJsonToType(token, paramType);
                }
                else if (paramInfo.HasDefaultValue)
                {
                    // デフォルト値を使用
                    parameters[i] = paramInfo.DefaultValue;
                }
                else
                {
                    throw new ArgumentException($"Required parameter '{paramName}' not provided");
                }
            }

            return parameters;
        }

        /// <summary>
        /// JSON トークンを指定の型に変換
        /// </summary>
        private object ConvertJsonToType(JToken token, Type targetType)
        {
            // Nullable 型の処理
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                if (token.Type == JTokenType.Null)
                {
                    return null;
                }
                targetType = underlyingType;
            }

            // 型に応じた変換
            if (targetType == typeof(string))
            {
                return token.ToString();
            }
            else if (targetType == typeof(int))
            {
                return token.Value<int>();
            }
            else if (targetType == typeof(long))
            {
                return token.Value<long>();
            }
            else if (targetType == typeof(double))
            {
                return token.Value<double>();
            }
            else if (targetType == typeof(float))
            {
                return token.Value<float>();
            }
            else if (targetType == typeof(bool))
            {
                return token.Value<bool>();
            }
            else if (targetType == typeof(decimal))
            {
                return token.Value<decimal>();
            }
            else if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                var array = token.ToObject(targetType);
                return array;
            }
            else if (targetType.IsGenericType)
            {
                var genericDef = targetType.GetGenericTypeDefinition();
                if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) || genericDef == typeof(IEnumerable<>))
                {
                    return token.ToObject(targetType);
                }
            }

            // その他の型は ToObject で変換
            return token.ToObject(targetType);
        }

        /// <summary>
        /// 戻り値を文字列に変換
        /// </summary>
        private string ConvertResultToString(object result, Type returnType)
        {
            if (result == null)
            {
                return "null";
            }

            // string はそのまま
            if (result is string str)
            {
                return str;
            }

            // プリミティブ型は ToString()
            if (returnType.IsPrimitive || returnType == typeof(decimal) ||
                returnType == typeof(DateTime) || returnType == typeof(Guid))
            {
                return result.ToString();
            }

            // 配列、List、匿名型などは JSON にシリアライズ
            try
            {
                return JsonConvert.SerializeObject(result);
            }
            catch
            {
                // シリアライズ失敗時は ToString()
                return result.ToString();
            }
        }
    }
}
