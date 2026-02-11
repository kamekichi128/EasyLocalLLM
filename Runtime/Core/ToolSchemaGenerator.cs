using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Utility for auto-generating JSON Schema from Delegate signatures
    /// </summary>
    public static class ToolSchemaGenerator
    {
        /// <summary>
        /// Auto-generate JSON Schema from Delegate signature
        /// </summary>
        /// <param name="callback">Callback function</param>
        /// <returns>JSON Schema (JObject)</returns>
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

                // Get parameter description (from ToolParameterAttribute)
                string description = paramName;
                var attr = param.GetCustomAttribute<ToolParameterAttribute>();
                if (attr != null)
                {
                    description = attr.Description;
                }

                // Generate JSON Schema property
                var propertySchema = CreatePropertySchema(paramType, description);
                schema["properties"][paramName] = propertySchema;

                // Add to required if no default value
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
        /// Generate JSON Schema property from parameter type
        /// </summary>
        private static JObject CreatePropertySchema(Type type, string description)
        {
            var schema = new JObject();

            // Handle Nullable types
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            // Generate schema based on type
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
                // Other types are treated as string
                schema["type"] = "string";
            }

            schema["description"] = description;

            return schema;
        }

        /// <summary>
        /// Get parameter information (for type conversion)
        /// </summary>
        /// <param name="callback">Callback function</param>
        /// <returns>List of parameter information</returns>
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
        /// Get return type
        /// </summary>
        /// <param name="callback">Callback function</param>
        /// <returns>Return type</returns>
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
