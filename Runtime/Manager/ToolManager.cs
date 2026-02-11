using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// Manager for tool registration, management, and execution
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
        /// Register tool (automatic schema generation)
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="callback">User-defined callback function</param>
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

            // Automatically generate JSON Schema
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
        /// Register tool (manual schema specification)
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="inputSchema">JSON Schema (JObject, string, or anonymous type)</param>
        /// <param name="callback">User-defined callback function</param>
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
                // Anonymous type or other object
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
        /// Unregister tool
        /// <param name="name">Tool name</param>
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
        /// Unregister all tools
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
        /// Get all registered tools
        /// </summary>
        public List<Core.ToolDefinition> GetAllTools()
        {
            return _tools.Values.ToList();
        }

        /// <summary>
        /// Check if tool is registered
        /// <param name="name">Tool name</param>
        /// </summary>
        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        /// <summary>
        /// Execute tool (with automatic type conversion)
        /// <param name="toolName">Tool name</param>
        /// <param name="argumentsJson">JSON string of arguments</param>
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

                // Parse JSON Arguments
                var argsJson = JObject.Parse(argumentsJson);

                // Convert parameters to target types
                var parameters = ConvertParameters(argsJson, toolDef.ParameterInfos);

                // Invoke the callback
                var result = toolDef.Callback.DynamicInvoke(parameters);

                // Convert result to string
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
        /// Convert JSON parameters to C# objects
        /// <param name="argsJson">Arguments in JObject format</param>
        /// <param name="paramInfos">Parameter information list</param>
        /// </summary>
        private object[] ConvertParameters(JObject argsJson, List<System.Reflection.ParameterInfo> paramInfos)
        {
            var parameters = new object[paramInfos.Count];

            for (int i = 0; i < paramInfos.Count; i++)
            {
                var paramInfo = paramInfos[i];
                var paramName = paramInfo.Name;
                var paramType = paramInfo.ParameterType;

                // Get value from JSON and convert to target type
                if (argsJson.TryGetValue(paramName, out var token))
                {
                    parameters[i] = ConvertJsonToType(token, paramType);
                }
                else if (paramInfo.HasDefaultValue)
                {
                    // Use default value if not provided
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
        /// Convert JSON token to specified C# type (handles nullable types, primitives, arrays, lists, etc.)
        /// <param name="token">JSON token to convert</param>
        /// <param name="targetType">Target C# type</param>
        /// </summary>
        private object ConvertJsonToType(JToken token, Type targetType)
        {
            // Handle Nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                if (token.Type == JTokenType.Null)
                {
                    return null;
                }
                targetType = underlyingType;
            }

            // Convert with specific handling for common types
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

            // Convert other types using ToObject
            return token.ToObject(targetType);
        }

        /// <summary>
        /// Convert result to string
        /// <param name="result">Result object</param>
        /// <param name="returnType">Return type of the tool</param>
        /// </summary>
        private string ConvertResultToString(object result, Type returnType)
        {
            if (result == null)
            {
                return "null";
            }

            // string is returned as is
            if (result is string str)
            {
                return str;
            }

            // Primitive types use ToString()
            if (returnType.IsPrimitive || returnType == typeof(decimal) ||
                returnType == typeof(DateTime) || returnType == typeof(Guid))
            {
                return result.ToString();
            }

            // Arrays, Lists, anonymous types, etc. are serialized to JSON
            try
            {
                return JsonConvert.SerializeObject(result);
            }
            catch
            {
                // If serialization fails, use ToString()
                return result.ToString();
            }
        }
    }
}
