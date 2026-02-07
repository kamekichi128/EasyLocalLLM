using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Delegate のシグネチャから JSON Schema を自動生成するユーティリティ
    /// </summary>
    public static class ToolSchemaGenerator
    {
        /// <summary>
        /// Delegate のシグネチャから JSON Schema を自動生成
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <returns>JSON Schema（JObject）</returns>
        public static JObject GenerateSchema(Delegate callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var methodInfo = callback.Method;
            var parameters = methodInfo.GetParameters();

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject()
            };

            var requiredList = new JArray();

            foreach (var param in parameters)
            {
                var paramName = param.Name;
                var paramType = param.ParameterType;
                var hasDefault = param.HasDefaultValue;

                // パラメータの説明を取得（ToolParameterAttribute から）
                string description = paramName;
                var attr = param.GetCustomAttribute<ToolParameterAttribute>();
                if (attr != null)
                {
                    description = attr.Description;
                }

                // JSON Schema のプロパティを生成
                var propertySchema = CreatePropertySchema(paramType, description);
                schema["properties"][paramName] = propertySchema;

                // デフォルト値がない場合は required に追加
                if (!hasDefault)
                {
                    requiredList.Add(paramName);
                }
            }

            if (requiredList.Count > 0)
            {
                schema["required"] = requiredList;
            }

            return schema;
        }

        /// <summary>
        /// パラメータの型から JSON Schema のプロパティを生成
        /// </summary>
        private static JObject CreatePropertySchema(Type type, string description)
        {
            var schema = new JObject();

            // Nullable 型の処理
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            // 型に応じたスキーマ生成
            if (type == typeof(string))
            {
                schema["type"] = "string";
            }
            else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                schema["type"] = "integer";
            }
            else if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                schema["type"] = "number";
            }
            else if (type == typeof(bool))
            {
                schema["type"] = "boolean";
            }
            else if (type.IsArray)
            {
                schema["type"] = "array";
                var elementType = type.GetElementType();
                schema["items"] = CreatePropertySchema(elementType, "item");
            }
            else if (type.IsGenericType &&
                     (type.GetGenericTypeDefinition() == typeof(List<>) ||
                      type.GetGenericTypeDefinition() == typeof(IList<>) ||
                      type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                schema["type"] = "array";
                var elementType = type.GetGenericArguments()[0];
                schema["items"] = CreatePropertySchema(elementType, "item");
            }
            else
            {
                // その他の型は string として扱う
                schema["type"] = "string";
            }

            schema["description"] = description;

            return schema;
        }

        /// <summary>
        /// パラメータ情報を取得（型変換用）
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <returns>パラメータ情報のリスト</returns>
        public static List<ParameterInfo> GetParameterInfos(Delegate callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var methodInfo = callback.Method;
            return methodInfo.GetParameters().ToList();
        }

        /// <summary>
        /// 戻り値の型を取得
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <returns>戻り値の型</returns>
        public static Type GetReturnType(Delegate callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return callback.Method.ReturnType;
        }
    }
}
