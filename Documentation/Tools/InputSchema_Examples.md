# inputSchema の具体的な指定方法

## 概要
inputSchema は **JSON Schema** 形式で、ツールが受け取るパラメータの構造を定義します。
Ollama/OpenAI の tools API 対応より。

---

## 基本構造

```json
{
  "type": "object",
  "properties": {
    "parameter_name": {
      "type": "パラメータの型",
      "description": "説明"
    }
  },
  "required": ["必須パラメータ"]
}
```

---

## 実装パターン

### **パターン1: 単純な文字列パラメータ（計算式の例）**

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
    callback: (input) =>
    {
        try
        {
            // input = "{\"expression\":\"5+3\"}"
            var json = Newtonsoft.Json.Linq.JObject.Parse(input);
            string expression = json["expression"].ToString();
            
            // 簡単な計算（実装例）
            var result = new System.Data.DataTable().Compute(expression, null);
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
);
```

---

### **パターン2: 複数パラメータ（Webスクレイピング的な例）**

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
                @enum = new[] { "en", "ja", "es", "fr" }  // 複数の場合
            }
        },
        required = new[] { "query" }  // max_results, language は省略可
    },
    callback: (input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string query = json["query"].ToString();
        int maxResults = json["max_results"]?.Value<int>() ?? 5;
        string language = json["language"]?.ToString() ?? "en";
        
        // 実装例: Webから検索結果を取得
        return $"Found results for '{query}' in {language}";
    }
);
```

---

### **パターン3: 数値範囲の指定**

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
                maximum = 100  // 最大100個まで
            }
        },
        required = new[] { "min", "max", "count" }
    },
    callback: (input) =>
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
    }
);
```

---

### **パターン4: クエリパラメータ形式（複数パラメータを一つの文字列として）**

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
                @enum = new[] { "celsius", "fahrenheit" }  // enum を使用
            }
        },
        required = new[] { "city" }
    },
    callback: (input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string city = json["city"].ToString();
        string unit = json["unit"]?.ToString() ?? "celsius";
        
        // 実装例
        return $"Weather in {city}: 20°{(unit == "celsius" ? "C" : "F")}";
    }
);
```

---

### **パターン5: 複雑なオブジェクト型（場所情報など）**

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
    callback: (input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        
        var from = json["location_from"];
        double lat1 = from["latitude"].Value<double>();
        double lon1 = from["longitude"].Value<double>();
        
        var to = json["location_to"];
        double lat2 = to["latitude"].Value<double>();
        double lon2 = to["longitude"].Value<double>();
        
        // Haversine 公式で距離計算（例）
        double distance = CalculateHaversineDistance(lat1, lon1, lat2, lon2);
        return $"{distance:F2} km";
    }
);
```

---

### **パターン6: 配列型パラメータ**

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
    callback: (input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        var texts = json["texts"].Values<string>().ToList();
        int maxLength = json["max_length"].Value<int>();
        
        string combined = string.Join(" ", texts);
        // 簡易要約（実装例）
        string summary = combined.Length > maxLength 
            ? combined.Substring(0, maxLength) + "..." 
            : combined;
        
        return summary;
    }
);
```

---

### **パターン7: 真偽値（ブール）パラメータ**

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
    callback: (input) =>
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(input);
        string text = json["text"].ToString();
        string targetLang = json["target_language"].ToString();
        bool formal = json["formal"]?.Value<bool>() ?? false;
        
        // 実装例
        return $"Translated to {targetLang} ({(formal ? "formal" : "casual")}): {text}";
    }
);
```

---

## JSON Schema のプロパティ一覧

| プロパティ | 説明 | 例 |
|----------|------|-----|
| `type` | データ型 | "string", "integer", "number", "boolean", "array", "object" |
| `description` | パラメータの説明 | "User's email address" |
| `minimum` | 数値の最小値 | `0` |
| `maximum` | 数値の最大値 | `100` |
| `minLength` | 文字列の最小長 | `1` |
| `maxLength` | 文字列の最大長 | `255` |
| `enum` | 取りうる値の列挙 | `new[] { "red", "green", "blue" }` |
| `required` | 必須パラメータのリスト | `new[] { "email", "name" }` |
| `minItems` | 配列の最小要素数 | `1` |
| `maxItems` | 配列の最大要素数 | `10` |
| `items` | 配列の要素の型定義 | `new { type = "string" }` |
| `properties` | オブジェクトのプロパティ定義 | (ネストされた定義) |
| `@enum` | C# で enum を使う場合 | `new[] { "option1", "option2" }` |

---

## LLM 側での理解

LLM は inputSchema を以下のように理解します：

```
Tool: calculator
Description: Evaluate a math expression...
Parameters:
  - expression (string, required): Math expression to evaluate...
```

つまり、**説明（description）が明確ほど、LLM が正確にツール呼び出しをします**。

---

## ベストプラクティス

1. **説明は詳細に** - 例を含めると効果的
   ```csharp
   description = "Email address in format 'user@example.com'"
   ```

2. **型制限を活用** - minimum/maximum/enum で LLM の誤りを減らす
   ```csharp
   type = "integer",
   minimum = 1,
   maximum = 100
   ```

3. **必須と省略可を明確に** - required に必須のみ入れる
   ```csharp
   required = new[] { "query" }  // language は省略可
   ```

4. **複雑さは避ける** - シンプルなスキーマほど LLM が正確
   ```csharp
   // ❌ 悪い例
   properties = new
   {
       nested_object = new { /* 深いネスト */ }
   }
   
   // ✅ 良い例
   properties = new
   {
       simple_string = new { type = "string" }
   }
   ```

