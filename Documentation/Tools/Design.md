# Tools (Function Calling) - Overview Guide

## What Are Tools (Function Calling)?

**Tools** (Tools / Function Calling) let the LLM access in-game features and data directly. When the LLM decides it needs an action like "check the player's gold" or "buy an item," it automatically calls a C# function you registered.

### Example

If the player says to an NPC, "I want to buy three health potions":

1. The LLM decides a purchase action is needed.
2. The registered `BuyItem` function is called automatically (`BuyItem("health_potion", 3)`).
3. The function returns a result (e.g., "Purchase successful. Gold left: 450G").
4. The LLM turns that result into a natural reply ("Bought three health potions. You have 450G left.").

This lets NPC conversations trigger real gameplay logic.

---

## Basic Usage

### 1. Register a Tool

Use `RegisterTool()` to make a C# function available to the LLM:

```csharp
// Register an addition tool
client.RegisterTool(
    name: "add_numbers",
    description: "Add two numbers together",
    callback: (Func<int, int, int>)((int a, int b) => a + b)
);
```

**Key points:**
- `name`: Tool name used by the LLM when calling it.
- `description`: Helps the LLM decide when to use the tool.
- `callback`: The function to execute (cast to `Func<...>`).

### 2. Send a Message

Send messages normally. If the LLM decides a tool is needed, it will call it automatically:

```csharp
StartCoroutine(client.SendMessageAsync(
    "What is 125 + 378?",
    response =>
    {
        Debug.Log(response.Content);
        // Example output: "125 + 378 = 503"
    }
));
```

---

## Architecture Overview
---

## Architecture Overview

High-level flow of how the LLM and game features connect:

```
Player -> LLM -> "Tool needed" -> Execute registered function -> Get result -> LLM final response
```

Detailed flow:

```
┌─────────────────────────────┐
│     OllamaClient            │
│  (with ToolManager)         │
├─────────────────────────────┤
│ - RegisterTool()            │  <- Tool registration
│ - SendMessageAsync()        │  <- Send message
│ - Tool auto-execution       │  <- LLM decides to call tools
└─────────────────────────────┘
        ↓
┌─────────────────────────────┐
│   Ollama API                │
├─────────────────────────────┤
│ - Request includes tools    │
│ - tool_calls in response    │
│ - Re-request with results   │
└─────────────────────────────┘
```

---

## Practical Usage Patterns

---

## Practical Usage Patterns

### Pattern 1: Simple Calculator Tool (1 parameter)

The simplest example. One parameter, and return values are converted automatically.

```csharp
// Tool that evaluates a math expression
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression like '5+3' or '10*2'",
    callback: (Func<string, object>)((string expression) =>
    {
        try
        {
            // Example implementation using DataTable
            return new System.Data.DataTable().Compute(expression, null);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);

// Example
// Player: "What is 5 + 3?"
// LLM calls calculator("5+3") -> 8 -> "5 + 3 is 8."
```

**Notes:**
- The schema is generated automatically.
- `object` return values are auto-stringified.

### Pattern 2: Multiple Parameters and Default Values

Multiple parameters with optional defaults.
```csharp
var client = new OllamaClient(config);

// Tool registration: calculator
// -> inputSchema auto-generated via reflection
// -> return value auto-converted (no ToString required)
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression",
    callback: (Func<string, object>)((string expression) =>
    {
        try
        {
            return new System.Data.DataTable().Compute(expression, null);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);
```

```csharp
// Web search tool (maxResults is optional)
client.RegisterTool(
    name: "search_web",
    description: "Search the web for information",
    callback: (Func<string, int, string>)((string query, int maxResults = 5) =>
    {
        var results = SearchEngine.Search(query, maxResults);
        return $"Found {results.Count} results for '{query}'";
    })
);

// Example
// Player: "Search Unity news for 3 items"
// LLM calls search_web("Unity", 3)
// Player: "Search about Python"
// LLM calls search_web("Python") with default maxResults=5
```

**Notes:**
- Parameters with default values are treated as optional by the LLM.
- Auto-generated schema excludes optional parameters from `required`.

### Pattern 3: Add Detailed Parameter Descriptions

Use `[ToolParameter]` to improve the LLM's accuracy.

```csharp
using EasyLocalLLM.LLM.Core;

// Weather tool
client.RegisterTool(
    name: "get_weather",
    description: "Get current weather information for a city",
    callback: (Func<string, string, string>)((
        [ToolParameter("City name (e.g., Tokyo, New York)")] string city,
        [ToolParameter("Temperature unit: celsius or fahrenheit")] string unit = "celsius"
    ) =>
    {
        var weather = WeatherAPI.GetWeather(city, unit);
        return $"Weather in {city}: {weather.Temperature} degrees {(unit == "celsius" ? "C" : "F")}, {weather.Condition}";
    })
);

// Example
// Player: "What's the weather in Tokyo in Fahrenheit?"
// LLM calls get_weather("Tokyo", "fahrenheit")
```

**Notes:**
- `[ToolParameter]` clarifies each parameter.
- Improves the LLM's tool selection and argument accuracy.

### Pattern 4: Primitive Return Types (Auto Conversion)

Return values like `int`, `bool`, and `double` are automatically converted to strings.

```csharp
// Addition tool (int return)
client.RegisterTool(
    name: "add_numbers",
    description: "Add two numbers together",
    callback: (Func<int, int, int>)((int a, int b) => a + b)
);

// Even check tool (bool return)
client.RegisterTool(
    name: "is_even",
    description: "Check if a number is even",
    callback: (Func<int, bool>)((int number) => number % 2 == 0)
);

// Division tool (double return)
client.RegisterTool(
    name: "divide",
    description: "Divide two numbers",
    callback: (Func<double, double, double>)((double a, double b) => a / b)
);

// Example
// Player: "What is 10 + 5?"
// LLM: add_numbers(10, 5) -> 15 -> "15"
// Player: "Is 8 even?"
// LLM: is_even(8) -> true -> "Yes, 8 is even."

**Notes:**
- No need to call `.ToString()`.
- The library converts values automatically.

### Pattern 5: Custom Object Return Types (JSON Conversion)

Objects and arrays are automatically serialized to JSON strings.

```csharp
// Player info tool (returns object)
client.RegisterTool(
    name: "get_player_info",
    description: "Get player information by player ID",
    callback: (Func<string, object>)((string playerId) =>
    {
        var player = PlayerManager.GetPlayer(playerId);
        return new
        {
            id = player.Id,
            name = player.Name,
            level = player.Level,
            gold = player.Gold,
            health = new { current = player.HP, max = player.MaxHP }
        };
    })
);

// Inventory tool (returns array)
client.RegisterTool(
    name: "get_inventory",
    description: "Get all items in player's inventory",
    callback: (Func<List<object>>)(() =>
    {
        return Inventory.GetAllItems().Select(item => new
        {
            id = item.Id,
            name = item.Name,
            quantity = item.Quantity
        }).ToList();
    })
);

// Example
// Player: "Show my status"
// LLM: get_player_info("player1") -> JSON string -> LLM turns into natural text
```

**Notes:**
- Objects and arrays are auto-converted to JSON.
- The LLM can interpret structured data into natural responses.

### Pattern 6: Practical In-Game Example

```csharp
public class NPCShopkeeper : MonoBehaviour
{
    private OllamaClient client;
    
    void Start()
    {
        client = LLMClientFactory.CreateOllamaClient(config);
        
        // Fetch shop inventory
        client.RegisterTool(
            name: "GetShopItems",
            description: "Get list of items available in the shop",
            callback: (Func<List<ShopItem>>)(() => ShopManager.GetAvailableItems())
        );
        
        // Buy an item
        client.RegisterTool(
            name: "BuyItem",
            description: "Buy an item from the shop",
            callback: (Func<string, int, string>)((string itemName, int quantity) =>
            {
                var result = ShopManager.BuyItem(itemName, quantity);
                if (result.Success)
                    return $"Purchased {quantity}x {itemName}. Remaining gold: {Player.Gold}";
                else
                    return $"Error: {result.ErrorMessage}";
            })
        );
    }
}

// Example
// Player: "What's for sale?"
// LLM calls GetShopItems() and explains the list
//
// Player: "Give me 3 health potions"
// LLM calls BuyItem("health_potion", 3)
```

---

## How Automatic Schema Generation Works

### What Automatic Generation Means

When you register a tool, you do not need to write `inputSchema` manually. The library uses reflection to generate it automatically.

### Supported Types

| C# Type | JSON Schema Type | Notes |
|-------|---------------|------|
| `string` | `string` | String |
| `int`, `long` | `integer` | Integer |
| `double`, `float` | `number` | Floating point |
| `bool` | `boolean` | Boolean |
| `List<T>`, `T[]` | `array` | Array |
| `DateTime` | `string` | ISO 8601 |
| `Guid` | `string` | GUID string |

### Example of Auto-Generated Schema

**Code:**
```csharp
client.RegisterTool(
    name: "search",
    description: "Search for items",
    callback: (Func<string, int, string>)((string query, int maxResults = 5) => "...")
);
```

**Generated schema:**
```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "query"
    },
    "maxResults": {
      "type": "integer",
      "description": "maxResults"
    }
  },
  "required": ["query"]
}
```

`maxResults` has a default value, so it is excluded from `required`.

### Manual Schema Definition

For complex inputs (nested objects, etc.), you can define the schema manually:

```csharp
client.RegisterTool(
    name: "create_character",
    description: "Create a new game character",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            name = new { type = "string", description = "Character name" },
            stats = new
            {
                type = "object",
                properties = new
                {
                    strength = new { type = "integer", minimum = 1, maximum = 10 },
                    agility = new { type = "integer", minimum = 1, maximum = 10 }
                },
                required = new[] { "strength", "agility" }
            }
        },
        required = new[] { "name", "stats" }
    },
    callback: (Func<string, string>)((string json) =>
    {
        // Parse JSON manually
        var data = JObject.Parse(json);
        // ...
        return "Character created";
    })
);
```

For more manual schema examples, see [InputSchema_Examples.md](InputSchema_Examples.md).

---

## Understanding the Execution Flow

### Basic Flow

```
1. Register tools
   RegisterTool() registers functions
   ↓
2. Send message
   Call SendMessageAsync("Question")
   ↓
3. LLM decides
   "This question needs a tool"
   ↓
4. Tool auto-execution
   Call the registered function
   ↓
5. Return results to LLM
   Re-request with tool results
   ↓
6. Final response
   LLM produces a natural reply
```

### Detailed Flow (Internal)

```
[User] SendMessageAsync("What is 125 + 378?")
    ↓
[OllamaClient] Build request
    - message: "What is 125 + 378?"
    - available tools: [add_numbers]
    ↓
[Ollama API] returns tool_call
    - tool_name: "add_numbers"
    - arguments: {"a": 125, "b": 378}
    ↓
[ToolManager] Execute tool
    - Convert JSON arguments to C# types
    - callback(125, 378)
    - return value: 503 (int)
    - int -> "503" (string)
    ↓
[OllamaClient] Re-request with result
    - message history
      - user: "What is 125 + 378?"
      - tool_result: "503"
    ↓
[Ollama API] Final response
    - "125 + 378 = 503"
    ↓
[User] Receives ChatResponse
```

### Multiple Tool Calls

The LLM can call tools multiple times if needed:

```
Player: "Buy 3 health potions and tell me the remaining gold"
↓
LLM: BuyItem("health_potion", 3)
→ "Purchase successful. Gold left: 450G"
↓
LLM: Final response
→ "Bought three health potions. You have 450G left."
```

To prevent infinite loops, a maximum iteration count is set (default: 5).

---

## Best Practices

### 1. Use Clear Tool Names and Descriptions

**Bad:**
```csharp
client.RegisterTool(
    name: "func1",
    description: "Does something",
    callback: ...
);
```

**Good:**
```csharp
client.RegisterTool(
    name: "get_player_health",
    description: "Get the current and maximum health points of a player by player ID",
    callback: ...
);
```

### 2. Add Parameter Descriptions

```csharp
client.RegisterTool(
    name: "move_character",
    description: "Move character to a location",
    callback: (Func<string, float, float, string>)((
        [ToolParameter("Character ID or name")] string characterId,
        [ToolParameter("X coordinate")] float x,
        [ToolParameter("Y coordinate")] float y
    ) => { ... })
);
```

### 3. Handle Errors in Tools

```csharp
client.RegisterTool(
    name: "buy_item",
    description: "Purchase an item",
    callback: (Func<string, int, string>)((string itemName, int quantity) =>
    {
        try
        {
            if (quantity <= 0)
                return "Error: Quantity must be positive";
            
            if (!Shop.HasItem(itemName))
                return $"Error: Item '{itemName}' not found in shop";
            
            if (Player.Gold < Shop.GetPrice(itemName) * quantity)
                return "Error: Not enough gold";
            
            Shop.BuyItem(itemName, quantity);
            return $"Success: Purchased {quantity}x {itemName}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);
```

### 4. Prefer Primitive Types

Auto schema generation works best with primitive types like `string`, `int`, and `bool`.

**Recommended:**
```csharp
callback: (Func<string, int, string>)((string itemName, int quantity) => ...)
```

**For complex types, use a manual schema:**
```csharp
// For custom classes, use manual schema and parse JSON manually
callback: (Func<string, string>)((string json) =>
{
    var data = JsonConvert.DeserializeObject<ComplexData>(json);
    ...
})
```

### 5. Check Auto-Generated Schema with DebugMode

Enable `DebugMode = true` to log the auto-generated schema during development.

```csharp
var config = new OllamaConfig
{
    DebugMode = true
};
```

---

## Troubleshooting

### Q1: Tool is not being called

**Cause:**
- Tool description is too vague
- LLM does not realize the tool is needed

**Fix:**
- Write a more detailed description
- Encourage tool use in the system prompt

```csharp
var options = new ChatRequestOptions
{
    SystemPrompt = "You are a helpful assistant. " +
                   "When the user asks about their inventory, use the get_inventory tool. " +
                   "When they want to buy items, use the buy_item tool."
};
```

### Q2: Type conversion errors

**Cause:**
- The LLM passes unexpected argument types

**Fix:**
- Check logs with `DebugMode`
- Clarify parameter types with `[ToolParameter]`

```csharp
callback: (Func<string, int, string>)((
    [ToolParameter("Player ID (must be a string)")] string playerId,
    [ToolParameter("Amount (must be a positive integer)")] int amount
) => ...)
```

### Q3: Infinite tool loop

**Cause:**
- The LLM keeps calling the same tool repeatedly

**Fix:**
- Adjust `MaxToolIterations`
- Ensure tools return clear results

```csharp
var options = new ChatRequestOptions
{
    MaxToolIterations = 3  // Default is 5
};
```

### Q4: Need complex object input

**Cause:**
- Auto schema generation is optimized for primitive types

**Fix:**
- Provide a manual schema and parse JSON input

```csharp
client.RegisterTool(
    name: "complex_tool",
    description: "Tool with complex input",
    inputSchema: new { /* manual schema */ },
    callback: (Func<string, string>)((string json) =>
    {
        var data = JObject.Parse(json);
        // ...
    })
);
```

See [InputSchema_Examples.md](InputSchema_Examples.md) for more details.

---

## Summary

With EasyLocalLLM tools:

- The LLM can call gameplay features directly
- Schemas are auto-generated to reduce boilerplate
- Return values are auto-converted
- Complex game logic can be combined with natural conversation

**Next steps:**
- [InputSchema_Examples.md](InputSchema_Examples.md) - Manual schema examples
- [Samples/SimpleChat.cs](../../Samples/SimpleChat.cs) - Implementation sample
- [API_Reference.md](../API_Reference.md) - Full API reference