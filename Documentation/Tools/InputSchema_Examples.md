# How to Define inputSchema

## Overview

`inputSchema` uses **JSON Schema** to define the structure of parameters a tool receives.
This follows the Ollama/OpenAI tools API.

---

## Basic Structure

```json
{
  "type": "object",
  "properties": {
    "parameter_name": {
      "type": "parameter type",
      "description": "Description"
    }
  },
  "required": ["required parameter"]
}
```

---

## Implementation Patterns

### **Pattern 1: Simple string parameter (expression example)**

```csharp
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression like '5+3' or '10*2'",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            expression = new
            {
                type = "string",
                description = "Math expression to evaluate (e.g., '5+3', '10*2')"
            }
        },
        required = new[] { "expression" }
    },
    callback: (Func<object, string>)((input) =>
    {
        try
        {
            // input = "{\"expression\":\"5+3\"}"
            var json = Newtonsoft.Json.Linq.JObject.Parse(input);
            string expression = json["expression"].ToString();
            
            // Simple calculation example
            var result = new System.Data.DataTable().Compute(expression, null);
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);
```

---

### **Pattern 2: Multiple parameters (web search style)**

```csharp
client.RegisterTool(
    name: "search_web",
    description: "Search the web for information",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "Search keywords"
            },
            max_results = new
            {
                type = "integer",
                description = "Maximum number of results (default: 5)"
            },
            language = new
            {
                type = "string",
                description = "Language code (e.g., 'en', 'ja')",
                @enum = new[] { "en", "ja", "es", "fr" }
            }
        },
        required = new[] { "query" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string query = json["query"].ToString();
        int maxResults = json["max_results"]?.Value<int>() ?? 5;
        string language = json["language"]?.ToString() ?? "en";
        
        // Example implementation
        return $"Found results for '{query}' in {language}";
    })
);
```

---

### **Pattern 3: Numeric ranges**

```csharp
client.RegisterTool(
    name: "generate_random",
    description: "Generate random numbers",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            min = new
            {
                type = "integer",
                description = "Minimum value",
                minimum = -1000000
            },
            max = new
            {
                type = "integer",
                description = "Maximum value",
                maximum = 1000000
            },
            count = new
            {
                type = "integer",
                description = "How many random numbers to generate",
                minimum = 1,
                maximum = 100
            }
        },
        required = new[] { "min", "max", "count" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        int min = json["min"].Value<int>();
        int max = json["max"].Value<int>();
        int count = json["count"].Value<int>();
        
        var rand = new System.Random();
        var numbers = Enumerable.Range(0, count)
            .Select(_ => rand.Next(min, max + 1))
            .ToList();
        
        return string.Join(", ", numbers);
    })
);
```

---

### **Pattern 4: Query-parameter style (multiple params)**

```csharp
client.RegisterTool(
    name: "get_weather",
    description: "Get weather information for a city",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            city = new
            {
                type = "string",
                description = "City name (e.g., 'Tokyo', 'New York')"
            },
            unit = new
            {
                type = "string",
                description = "Temperature unit",
                @enum = new[] { "celsius", "fahrenheit" }
            }
        },
        required = new[] { "city" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string city = json["city"].ToString();
        string unit = json["unit"]?.ToString() ?? "celsius";
        
        // Example implementation
        return $"Weather in {city}: 20{(unit == "celsius" ? "C" : "F")}";
    })
);
```

---

### **Pattern 5: Complex object (location data)**

```csharp
client.RegisterTool(
    name: "calculate_distance",
    description: "Calculate distance between two locations",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            location_from = new
            {
                type = "object",
                description = "Starting location",
                properties = new
                {
                    latitude = new { type = "number", description = "Latitude" },
                    longitude = new { type = "number", description = "Longitude" }
                },
                required = new[] { "latitude", "longitude" }
            },
            location_to = new
            {
                type = "object",
                description = "Destination location",
                properties = new
                {
                    latitude = new { type = "number", description = "Latitude" },
                    longitude = new { type = "number", description = "Longitude" }
                },
                required = new[] { "latitude", "longitude" }
            }
        },
        required = new[] { "location_from", "location_to" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        
        var from = json["location_from"];
        double lat1 = from["latitude"].Value<double>();
        double lon1 = from["longitude"].Value<double>();
        
        var to = json["location_to"];
        double lat2 = to["latitude"].Value<double>();
        double lon2 = to["longitude"].Value<double>();
        
        // Haversine formula example
        double distance = CalculateHaversineDistance(lat1, lon1, lat2, lon2);
        return $"{distance:F2} km";
    })
);
```

---

### **Pattern 6: Array parameters**

```csharp
client.RegisterTool(
    name: "summarize_text",
    description: "Summarize multiple text documents",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            texts = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of texts to summarize",
                minItems = 1,
                maxItems = 10
            },
            max_length = new
            {
                type = "integer",
                description = "Maximum length of summary in words",
                minimum = 10,
                maximum = 1000
            }
        },
        required = new[] { "texts", "max_length" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        var texts = json["texts"].Values<string>().ToList();
        int maxLength = json["max_length"].Value<int>();
        
        string combined = string.Join(" ", texts);
        // Simple summary example
        string summary = combined.Length > maxLength
            ? combined.Substring(0, maxLength) + "..."
            : combined;
        
        return summary;
    })
);
```

---

### **Pattern 7: Boolean parameter**

```csharp
client.RegisterTool(
    name: "translate_text",
    description: "Translate text to another language",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            text = new
            {
                type = "string",
                description = "Text to translate"
            },
            target_language = new
            {
                type = "string",
                description = "Target language (e.g., 'en', 'ja', 'es')"
            },
            formal = new
            {
                type = "boolean",
                description = "Use formal language style"
            }
        },
        required = new[] { "text", "target_language" }
    },
    callback: (Func<object, string>)((input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string text = json["text"].ToString();
        string targetLang = json["target_language"].ToString();
        bool formal = json["formal"]?.Value<bool>() ?? false;
        
        // Example implementation
        return $"Translated to {targetLang} ({(formal ? "formal" : "casual")}): {text}";
    })
);
```

---

## JSON Schema Property Reference

| Property | Description | Example |
|----------|-------------|---------|
| `type` | Data type | "string", "integer", "number", "boolean", "array", "object" |
| `description` | Parameter description | "User's email address" |
| `minimum` | Minimum numeric value | `0` |
| `maximum` | Maximum numeric value | `100` |
| `minLength` | Minimum string length | `1` |
| `maxLength` | Maximum string length | `255` |
| `enum` | Allowed values | `new[] { "red", "green", "blue" }` |
| `required` | Required parameters | `new[] { "email", "name" }` |
| `minItems` | Minimum array length | `1` |
| `maxItems` | Maximum array length | `10` |
| `items` | Array item schema | `new { type = "string" }` |
| `properties` | Object properties | (nested definitions) |
| `@enum` | Use in C# for enum values | `new[] { "option1", "option2" }` |

---

## How the LLM Interprets inputSchema

The LLM interprets inputSchema like this:

```
Tool: calculator
Description: Evaluate a math expression...
Parameters:
  - expression (string, required): Math expression to evaluate...
```

In short, **clear descriptions lead to more accurate tool calls**.

---

## Best Practices

1. **Be specific in descriptions** - examples help
   ```csharp
   description = "Email address in format 'user@example.com'"
   ```

2. **Use constraints** - minimum/maximum/enum reduce errors
   ```csharp
   type = "integer",
   minimum = 1,
   maximum = 100
   ```

3. **Separate required and optional** - include only required in `required`
   ```csharp
   required = new[] { "query" }  // language is optional
   ```

4. **Avoid complexity** - simpler schemas work best
   ```csharp
   // Bad
   properties = new
   {
       nested_object = new { /* deep nesting */ }
   }
   
   // Good
   properties = new
   {
       simple_string = new { type = "string" }
   }
   ```