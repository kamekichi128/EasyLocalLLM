(function () {
    var state = {
        config: null,
        initialized: false,
        runtime: null,
        sessions: {}
    };

    function parseModelUrl(modelUrl) {
        if (!modelUrl) {
            return null;
        }

        if (/^https?:\/\//i.test(modelUrl) || modelUrl.startsWith("blob:") || modelUrl.startsWith("data:")) {
            return modelUrl;
        }

        if (modelUrl.startsWith("StreamingAssets/")) {
            return modelUrl;
        }

        return modelUrl;
    }

    function getWllamaCtorFromGlobal() {
        if (typeof window.Wllama === "function") {
            return window.Wllama;
        }

        if (window.wllama && typeof window.wllama.Wllama === "function") {
            return window.wllama.Wllama;
        }

        return null;
    }

    function buildWllamaModuleCandidates(cfg) {
        var candidates = [];

        if (cfg && typeof cfg.wllamaModuleUrl === "string" && cfg.wllamaModuleUrl.length > 0) {
            candidates.push(cfg.wllamaModuleUrl);
        }

        var wasmBaseUrl = (cfg && cfg.wasmBaseUrl ? cfg.wasmBaseUrl : "StreamingAssets/llama-wasm").replace(/\/$/, "");
        candidates.push(wasmBaseUrl + "/index.js");
        candidates.push(wasmBaseUrl + "/index.mjs");
        candidates.push("https://cdn.jsdelivr.net/npm/@wllama/wllama@latest/esm/index.js");

        var deduped = [];
        for (var i = 0; i < candidates.length; i++) {
            if (deduped.indexOf(candidates[i]) < 0) {
                deduped.push(candidates[i]);
            }
        }

        return deduped;
    }

    function toAbsoluteUrl(url) {
        if (!url) {
            return url;
        }

        if (/^(https?:|file:|blob:|data:)/i.test(url)) {
            return url;
        }

        if (typeof URL === "function" && typeof window !== "undefined" && window.location) {
            return new URL(url, window.location.href).href;
        }

        return url;
    }

    async function ensureWllamaCtor(cfg) {
        var candidates = buildWllamaModuleCandidates(cfg || {});
        var errors = [];

        for (var i = 0; i < candidates.length; i++) {
            var candidate = candidates[i];
            var resolved = toAbsoluteUrl(candidate);

            try {
                var moduleNs = await import(/* webpackIgnore: true */ resolved);
                var ctor = null;

                if (moduleNs && typeof moduleNs.Wllama === "function") {
                    ctor = moduleNs.Wllama;
                } else if (moduleNs && moduleNs.default && typeof moduleNs.default.Wllama === "function") {
                    ctor = moduleNs.default.Wllama;
                }

                if (ctor) {
                    if (typeof window !== "undefined") {
                        window.Wllama = window.Wllama || ctor;
                        window.wllama = window.wllama || moduleNs;
                    }

                    return ctor;
                }

                errors.push("Imported but Wllama export missing: " + resolved);
            } catch (error) {
                errors.push(resolved + " -> " + (error && error.message ? error.message : String(error)));
            }
        }

        var globalCtor = getWllamaCtorFromGlobal();
        if (globalCtor) {
            return globalCtor;
        }

        throw new Error("Wllama runtime is not found. Tried: " + candidates.join(", ") + " | Errors: " + errors.join(" ; "));
    }

    function buildPrompt(messages) {
        if (!messages || !messages.length) {
            return "";
        }

        var lines = [];
        for (var i = 0; i < messages.length; i++) {
            var message = messages[i] || {};
            var role = (message.role || "user").toLowerCase();
            var content = message.content || "";

            if (role === "system") {
                lines.push("<|system|>\n" + content + "\n</s>");
            } else if (role === "assistant") {
                lines.push("<|assistant|>\n" + content + "\n</s>");
            } else {
                lines.push("<|user|>\n" + content + "\n</s>");
            }
        }

        lines.push("<|assistant|>\n");
        return lines.join("\n");
    }

    function buildChatMessages(messages) {
        if (!messages || !messages.length) {
            return [];
        }

        var result = [];
        for (var i = 0; i < messages.length; i++) {
            var message = messages[i] || {};
            var role = (message.role || "user").toLowerCase();
            if (role !== "system" && role !== "assistant" && role !== "user") {
                role = "user";
            }

            result.push({
                role: role,
                content: message.content || ""
            });
        }

        return result;
    }

    function parseResponseFormat(req) {
        var opts = (req && req.options) || {};
        var rawFormat = typeof opts.format === "string" ? opts.format.toLowerCase() : "";
        var hasSchema = typeof opts.format_schema !== "undefined" && opts.format_schema !== null;

        return {
            isJsonRequested: rawFormat === "json" || hasSchema,
            format: rawFormat,
            formatSchema: hasSchema ? opts.format_schema : null
        };
    }

    function stringifySchemaForPrompt(schema) {
        if (!schema) {
            return "";
        }

        if (typeof schema === "string") {
            return schema;
        }

        try {
            return JSON.stringify(schema, null, 2);
        } catch (_schemaStringifyError) {
            return String(schema);
        }
    }

    function buildJsonFormatInstruction(responseFormat) {
        if (!responseFormat || !responseFormat.isJsonRequested) {
            return "";
        }

        var baseInstruction = "You must output only valid JSON. Do not include markdown fences, explanations, or extra text.";
        if (!responseFormat.formatSchema) {
            return baseInstruction;
        }

        var schemaText = stringifySchemaForPrompt(responseFormat.formatSchema);
        return baseInstruction + " Follow this JSON Schema exactly:\n" + schemaText;
    }

    function applyResponseFormatToMessages(messages, responseFormat) {
        var formatInstruction = buildJsonFormatInstruction(responseFormat);
        if (!formatInstruction) {
            return messages;
        }

        var formatted = (messages || []).slice();
        formatted.unshift({
            role: "system",
            content: formatInstruction
        });

        return formatted;
    }

    function applyResponseFormatToPrompt(prompt, responseFormat) {
        var formatInstruction = buildJsonFormatInstruction(responseFormat);
        if (!formatInstruction) {
            return prompt;
        }

        return "<|system|>\n" + formatInstruction + "\n</s>\n" + (prompt || "");
    }

    function buildRuntimeFormatOptions(responseFormat) {
        if (!responseFormat || !responseFormat.isJsonRequested) {
            return null;
        }

        var formatValue = responseFormat.formatSchema ? responseFormat.formatSchema : "json";
        return {
            format: formatValue,
            response_format: formatValue
        };
    }

    function parseSchemaObject(formatSchema) {
        if (!formatSchema) {
            return null;
        }

        if (typeof formatSchema === "string") {
            try {
                return JSON.parse(formatSchema);
            } catch (_schemaParseError) {
                return null;
            }
        }

        if (typeof formatSchema === "object") {
            return formatSchema;
        }

        return null;
    }

    function escapeGrammarString(text) {
        if (typeof text !== "string") {
            text = String(text);
        }

        return text
            .replace(/\\/g, "\\\\")
            .replace(/"/g, "\\\"")
            .replace(/\n/g, "\\n")
            .replace(/\r/g, "\\r")
            .replace(/\t/g, "\\t");
    }

    function createSchemaGrammarBuilder() {
        return {
            rules: [],
            ruleMap: {},
            counter: 0,
            add: function (name, body) {
                var existing = this.ruleMap[name];
                if (existing === body) {
                    return;
                }

                if (typeof existing === "string" && existing !== body) {
                    return;
                }

                this.ruleMap[name] = body;
                this.rules.push(name + " ::= " + body);
            },
            nextName: function (prefix) {
                this.counter += 1;
                return prefix + "_" + this.counter;
            }
        };
    }

    function buildSchemaEnumAlternatives(schema) {
        if (!schema || !Array.isArray(schema.enum) || schema.enum.length === 0) {
            return null;
        }

        var literals = [];
        for (var i = 0; i < schema.enum.length; i++) {
            var value = schema.enum[i];
            if (typeof value === "string") {
                literals.push("\"\\\"" + escapeGrammarString(value) + "\\\"\"");
            } else if (typeof value === "number") {
                literals.push("\"" + String(value) + "\"");
            } else if (typeof value === "boolean") {
                literals.push(value ? "\"true\"" : "\"false\"");
            } else if (value === null) {
                literals.push("\"null\"");
            }
        }

        return literals.length > 0 ? literals : null;
    }

    function appendBaseJsonGrammarRules(builder) {
        builder.add("value", "object | array | string | number | \"true\" | \"false\" | \"null\"");
        builder.add("object", "\"{\" ws (string ws \":\" ws value (ws \",\" ws string ws \":\" ws value)*)? \"}\" ws");
        builder.add("array", "\"[\" ws (value (ws \",\" ws value)*)? \"]\" ws");
        builder.add("string", "\"\\\"\" chars \"\\\"\" ws");
        builder.add("chars", "char chars |");
        builder.add("char", "[^\"\\\\] | \\\"\\\\\\\" ([\"\\\\/bfnrt] | \"u\" hex hex hex hex)");
        builder.add("hex", "[0-9a-fA-F]");
        builder.add("number", "int frac? exp? ws");
        builder.add("int", "\"-\"? (\"0\" | [1-9] [0-9]*)");
        builder.add("frac", "\".\" [0-9]+");
        builder.add("exp", "([eE] [+-]? [0-9]+)");
        builder.add("ws", "[ \\t\\n\\r]*");
    }

    function buildSchemaValueRule(schema, builder, ruleName, depth, forceStrictObject) {
        if (depth > 6) {
            builder.add(ruleName, "value");
            return;
        }

        var enumAlternatives = buildSchemaEnumAlternatives(schema);
        if (enumAlternatives && enumAlternatives.length > 0) {
            builder.add(ruleName, "(" + enumAlternatives.join(" | ") + ") ws");
            return;
        }

        var typeName = schema && typeof schema.type === "string" ? schema.type : "";

        if (typeName === "string") {
            builder.add(ruleName, "string");
            return;
        }

        if (typeName === "integer") {
            builder.add(ruleName, "int ws");
            return;
        }

        if (typeName === "number") {
            builder.add(ruleName, "number");
            return;
        }

        if (typeName === "boolean") {
            builder.add(ruleName, "(\"true\" | \"false\") ws");
            return;
        }

        if (typeName === "null") {
            builder.add(ruleName, "\"null\" ws");
            return;
        }

        if (typeName === "array") {
            var itemRule = builder.nextName("schema_item");
            buildSchemaValueRule(schema ? schema.items : null, builder, itemRule, depth + 1, false);
            builder.add(ruleName, "\"[\" ws (" + itemRule + " (ws \",\" ws " + itemRule + ")*)? \"]\" ws");
            return;
        }

        if (typeName === "object") {
            buildSchemaObjectRule(schema, builder, ruleName, depth + 1, !!forceStrictObject || (schema && schema.additionalProperties === false));
            return;
        }

        builder.add(ruleName, "value");
    }

    function buildSchemaObjectRule(schema, builder, ruleName, depth, strictMode) {
        var properties = schema && schema.properties && typeof schema.properties === "object"
            ? schema.properties
            : null;

        if (!properties) {
            builder.add(ruleName, "object");
            return;
        }

        var keys = Object.keys(properties);
        var required = Array.isArray(schema.required) ? schema.required.slice() : [];
        var requiredSet = {};

        for (var i = 0; i < required.length; i++) {
            if (!properties[required[i]]) {
                builder.add(ruleName, "object");
                return;
            }
            requiredSet[required[i]] = true;
        }

        if (!strictMode) {
            builder.add(ruleName, "object");
            return;
        }

        var requiredPairs = [];
        var optionalPairs = [];

        for (var k = 0; k < keys.length; k++) {
            var keyName = keys[k];
            var valueRule = builder.nextName("schema_value");
            buildSchemaValueRule(properties[keyName], builder, valueRule, depth + 1, false);

            var pairRule = builder.nextName("schema_pair");
            builder.add(pairRule, "\"\\\"" + escapeGrammarString(keyName) + "\\\"\" ws \":\" ws " + valueRule);

            if (requiredSet[keyName]) {
                requiredPairs.push(pairRule);
            } else {
                optionalPairs.push(pairRule);
            }
        }

        if (requiredPairs.length === 0 && optionalPairs.length === 0) {
            builder.add(ruleName, "\"{\" ws \"}\" ws");
            return;
        }

        if (requiredPairs.length > 0) {
            var requiredSequence = requiredPairs.join(" ws \",\" ws ");
            if (optionalPairs.length > 0) {
                var optionalChoice = builder.nextName("schema_optional");
                builder.add(optionalChoice, optionalPairs.join(" | "));
                builder.add(ruleName, "\"{\" ws " + requiredSequence + " (ws \",\" ws " + optionalChoice + ")* \"}\" ws");
                return;
            }

            builder.add(ruleName, "\"{\" ws " + requiredSequence + " \"}\" ws");
            return;
        }

        var optionalOnlyChoice = builder.nextName("schema_optional");
        builder.add(optionalOnlyChoice, optionalPairs.join(" | "));
        builder.add(ruleName, "\"{\" ws (" + optionalOnlyChoice + " (ws \",\" ws " + optionalOnlyChoice + ")*)? \"}\" ws");
    }

    function buildSchemaObjectGrammar(schema) {
        var parsed = parseSchemaObject(schema);
        if (!parsed || parsed.type !== "object") {
            return null;
        }

        var builder = createSchemaGrammarBuilder();
        builder.add("root", "schema_root");
        buildSchemaValueRule(parsed, builder, "schema_root", 0, true);
        appendBaseJsonGrammarRules(builder);

        return builder.rules.join("\n");
    }

    function buildJsonGrammar(responseFormat) {
        if (!responseFormat || !responseFormat.isJsonRequested) {
            return null;
        }

        if (responseFormat.formatSchema) {
            return null;
        }

        return [
            "root ::= value",
            "value ::= object | array | string | number | \"true\" | \"false\" | \"null\"",
            "object ::= \"{\" ws (string \":\" ws value (\",\" ws string \":\" ws value)*)? \"}\" ws",
            "array ::= \"[\" ws (value (\",\" ws value)*)? \"]\" ws",
            "string ::= \"\\\"\" chars \"\\\"\" ws",
            "chars ::= ([^\"\\\\] | \\\"\\\\\\\" ([\"\\\\/bfnrt] | \"u\" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F]))*",
            "number ::= (\"-\"? ([0-9] | [1-9] [0-9]*)) (\".\" [0-9]+)? ([eE] [+-]? [0-9]+)? ws",
            "ws ::= ([ \\t\\n\\r] ws)?"
        ].join("\n");
    }

    function buildSamplingConfigForRuntime(sampling, responseFormat) {
        var samplingConfig = {
            temp: sampling.temperature,
            top_p: sampling.topP,
            top_k: sampling.topK,
            min_p: sampling.minP,
            seed: sampling.seed
        };

        var grammar = buildJsonGrammar(responseFormat);
        if (grammar) {
            samplingConfig.grammar = grammar;
        }

        return samplingConfig;
    }

    function buildSamplingConfigWithoutGrammar(samplingConfig) {
        var copy = {
            temp: samplingConfig.temp,
            top_p: samplingConfig.top_p,
            top_k: samplingConfig.top_k,
            min_p: samplingConfig.min_p,
            seed: samplingConfig.seed
        };

        return copy;
    }

    function isGrammarSamplerRuntimeFailure(error) {
        if (!error) {
            return false;
        }

        var message = "";
        if (typeof error === "string") {
            message = error;
        } else if (error && typeof error.message === "string") {
            message = error.message;
        } else {
            message = String(error);
        }

        return /GGML_ASSERT|llama-sampling|unreachable|sampling/i.test(message);
    }

    async function ensureRuntime() {
        if (state.runtime) {
            return state.runtime;
        }

        var cfg = state.config || {};
        var WllamaCtor = await ensureWllamaCtor(cfg);
        var wasmBaseUrl = cfg.wasmBaseUrl || "StreamingAssets/llama-wasm";
        var normalizedWasmBaseUrl = wasmBaseUrl.replace(/\/$/, "");
        var logger = cfg.debugMode ? {
            debug: function () { console.debug.apply(console, ["[EasyLocalLLM][wllama]"].concat(Array.prototype.slice.call(arguments))); },
            log: function () { console.log.apply(console, ["[EasyLocalLLM][wllama]"].concat(Array.prototype.slice.call(arguments))); },
            info: function () { console.info.apply(console, ["[EasyLocalLLM][wllama]"].concat(Array.prototype.slice.call(arguments))); },
            warn: function () { console.warn.apply(console, ["[EasyLocalLLM][wllama]"].concat(Array.prototype.slice.call(arguments))); },
            error: function () { console.error.apply(console, ["[EasyLocalLLM][wllama]"].concat(Array.prototype.slice.call(arguments))); }
        } : undefined;

        var ctorOptions = {};
        if (logger) {
            ctorOptions.logger = logger;
        }

        if (!cfg.disableCache && typeof window !== "undefined" && window.wllama && typeof window.wllama.CacheManager === "function") {
            try {
                ctorOptions.cacheManager = new window.wllama.CacheManager();
            } catch (_cacheError) {
            }
        }

        var pathConfig = {
            "single-thread/wllama.wasm": normalizedWasmBaseUrl + "/single-thread/wllama.wasm"
        };

        pathConfig["multi-thread/wllama.wasm"] = normalizedWasmBaseUrl + "/multi-thread/wllama.wasm";

        var runtime = new WllamaCtor(pathConfig, ctorOptions);

        var modelUrl = parseModelUrl(cfg.modelUrl);
        if (!modelUrl) {
            throw new Error("modelUrl is required.");
        }

        var loadOptions = {
            n_ctx: typeof cfg.contextSize === "number" && cfg.contextSize > 0 ? cfg.contextSize : 2048
        };

        if (typeof runtime.loadModelFromUrl === "function") {
            await runtime.loadModelFromUrl(modelUrl, loadOptions);
        } else if (typeof runtime.loadModel === "function") {
            await runtime.loadModel(modelUrl, loadOptions);
        } else {
            throw new Error("Unsupported Wllama API. Expected loadModelFromUrl or loadModel.");
        }

        state.runtime = runtime;
        return runtime;
    }

    function buildSampling(req) {
        var opts = (req && req.options) || {};
        return {
            nPredict: typeof opts.n_predict === "number" ? opts.n_predict : 256,
            temperature: typeof opts.temperature === "number" ? opts.temperature : 0.7,
            topP: typeof opts.top_p === "number" ? opts.top_p : undefined,
            topK: typeof opts.top_k === "number" ? opts.top_k : undefined,
            minP: typeof opts.min_p === "number" ? opts.min_p : undefined,
            seed: typeof opts.seed === "number" ? opts.seed : undefined,
            stopTokens: Array.isArray(opts.stop) ? opts.stop : undefined
        };
    }

    function normalizeAbortError() {
        var e = new Error("Request aborted.");
        e.name = "AbortError";
        return e;
    }

    function createPieceDecoder() {
        var textDecoder = (typeof TextDecoder !== "undefined") ? new TextDecoder("utf-8") : null;

        function decodeBytes(bytes) {
            if (!bytes || bytes.length === 0) {
                return "";
            }

            if (textDecoder) {
                try {
                    return textDecoder.decode(bytes, { stream: true });
                } catch (_decodeError) {
                }
            }

            var text = "";
            for (var i = 0; i < bytes.length; i++) {
                text += String.fromCharCode(bytes[i]);
            }
            return text;
        }

        return function pieceToText(piece) {
            if (piece == null) {
                return "";
            }

            if (typeof piece === "string") {
                return piece;
            }

            if (piece instanceof Uint8Array) {
                return decodeBytes(piece);
            }

            if (Array.isArray(piece)) {
                return decodeBytes(new Uint8Array(piece));
            }

            if (piece && piece.buffer instanceof ArrayBuffer && typeof piece.byteLength === "number") {
                try {
                    return decodeBytes(new Uint8Array(piece.buffer, piece.byteOffset || 0, piece.byteLength));
                } catch (_typedArrayError) {
                }
            }

            return String(piece);
        };
    }

    function stripControlTokens(text) {
        if (!text) {
            return "";
        }

        return text
            .replace(/<\|[^<>|]+\|>/g, "")
            .replace(/<\/?s>/g, "");
    }

    function findEarliestIndex(text, needles) {
        var minIndex = -1;
        for (var i = 0; i < needles.length; i++) {
            var idx = text.indexOf(needles[i]);
            if (idx >= 0 && (minIndex < 0 || idx < minIndex)) {
                minIndex = idx;
            }
        }
        return minIndex;
    }

    function createTurnBoundarySanitizer() {
        var markers = ["<|user|>", "<|assistant|>", "<|system|>", "<|im_start|>", "<|im_end|>", "</s>", "<s>"];
        var maxMarkerLength = 0;
        for (var i = 0; i < markers.length; i++) {
            maxMarkerLength = Math.max(maxMarkerLength, markers[i].length);
        }

        var pending = "";
        var ended = false;

        function splitPendingTail(text) {
            var searchFrom = Math.max(0, text.length - (maxMarkerLength - 1));
            var best = "";

            for (var i = searchFrom; i < text.length; i++) {
                if (text.charAt(i) !== "<") {
                    continue;
                }

                var tail = text.substring(i);
                for (var j = 0; j < markers.length; j++) {
                    if (markers[j].indexOf(tail) === 0) {
                        if (tail.length > best.length) {
                            best = tail;
                        }
                        break;
                    }
                }
            }

            if (!best) {
                return { head: text, tail: "" };
            }

            return {
                head: text.substring(0, text.length - best.length),
                tail: best
            };
        }

        return {
            push: function (text) {
                if (ended || !text) {
                    return "";
                }

                var combined = pending + text;
                pending = "";

                var markerAt = findEarliestIndex(combined, markers);
                if (markerAt >= 0) {
                    ended = true;
                    return stripControlTokens(combined.substring(0, markerAt));
                }

                var split = splitPendingTail(combined);
                pending = split.tail;
                return stripControlTokens(split.head);
            },
            flush: function () {
                if (ended) {
                    pending = "";
                    return "";
                }

                var rest = stripControlTokens(pending);
                pending = "";
                return rest;
            }
        };
    }

    async function generateWithRuntime(runtime, req, ctx) {
        var sampling = buildSampling(req);
        var responseFormat = parseResponseFormat(req);
        var messages = applyResponseFormatToMessages(req.messages || [], responseFormat);
        var prompt = applyResponseFormatToPrompt(buildPrompt(messages), responseFormat);
        var chatMessages = buildChatMessages(messages);
        var runtimeFormatOptions = buildRuntimeFormatOptions(responseFormat);
        var runtimeSamplingConfig = buildSamplingConfigForRuntime(sampling, responseFormat);
        var runtimeSamplingConfigNoGrammar = buildSamplingConfigWithoutGrammar(runtimeSamplingConfig);
        var content = "";
        var pieceToText = createPieceDecoder();
        var sanitizer = createTurnBoundarySanitizer();

        var aborted = false;
        var abortHandler = null;
        if (ctx && ctx.signal) {
            if (ctx.signal.aborted) {
                throw normalizeAbortError();
            }

            abortHandler = function () {
                aborted = true;
            };
            ctx.signal.addEventListener("abort", abortHandler);
        }

        async function runGenerationWithSampling(activeSamplingConfig) {
            if (typeof runtime.createChatCompletion === "function") {
                var chatOptions = {
                    nPredict: sampling.nPredict,
                    sampling: activeSamplingConfig,
                    stream: true
                };

                if (runtimeFormatOptions) {
                    chatOptions.format = runtimeFormatOptions.format;
                    chatOptions.response_format = runtimeFormatOptions.response_format;
                }

                var stream = await runtime.createChatCompletion(chatMessages, chatOptions);

                for await (var chunk of stream) {
                    if (aborted) {
                        throw normalizeAbortError();
                    }

                    if (!chunk) {
                        continue;
                    }

                    var text = sanitizer.push(pieceToText(chunk.piece));
                    if (!text) {
                        continue;
                    }

                    content += text;
                    if (ctx && typeof ctx.onChunk === "function") {
                        ctx.onChunk(text);
                    }
                }
            } else if (typeof runtime.createCompletion === "function") {
                var stopTokens = (sampling.stopTokens || []).slice();
                stopTokens.push("<|user|>", "<|assistant|>", "<|system|>", "</s>");

                var completionOptions = {
                    nPredict: sampling.nPredict,
                    sampling: activeSamplingConfig,
                    stop: stopTokens,
                    onNewToken: function (_token, piece) {
                        if (aborted) {
                            throw normalizeAbortError();
                        }

                        if (piece == null) {
                            return;
                        }

                        var text = sanitizer.push(pieceToText(piece));
                        if (!text) {
                            return;
                        }

                        content += text;
                        if (ctx && typeof ctx.onChunk === "function") {
                            ctx.onChunk(text);
                        }
                    }
                };

                if (runtimeFormatOptions) {
                    completionOptions.format = runtimeFormatOptions.format;
                    completionOptions.response_format = runtimeFormatOptions.response_format;
                }

                await runtime.createCompletion(prompt, completionOptions);
            } else if (typeof runtime.completion === "function") {
                var legacyCompletionOptions = {
                    n_predict: sampling.nPredict,
                    temperature: sampling.temperature,
                    top_p: sampling.topP,
                    top_k: sampling.topK,
                    min_p: sampling.minP,
                    grammar: activeSamplingConfig.grammar,
                    stop: sampling.stopTokens,
                    stream: true,
                    onToken: function (piece) {
                        if (aborted) {
                            throw normalizeAbortError();
                        }

                        if (piece == null) {
                            return;
                        }

                        var text = sanitizer.push(pieceToText(piece));
                        if (!text) {
                            return;
                        }

                        content += text;
                        if (ctx && typeof ctx.onChunk === "function") {
                            ctx.onChunk(text);
                        }
                    }
                };

                if (runtimeFormatOptions) {
                    legacyCompletionOptions.format = runtimeFormatOptions.format;
                    legacyCompletionOptions.response_format = runtimeFormatOptions.response_format;
                }

                var response = await runtime.completion(prompt, legacyCompletionOptions);

                if (!content && response && typeof response.content === "string") {
                    content = sanitizer.push(response.content) + sanitizer.flush();
                    if (ctx && typeof ctx.onChunk === "function") {
                        ctx.onChunk(content);
                    }
                }
            } else {
                throw new Error("Unsupported Wllama API. Expected createCompletion or completion.");
            }
        }

        try {
            try {
                await runGenerationWithSampling(runtimeSamplingConfig);
            } catch (firstError) {
                if (runtimeSamplingConfig.grammar && isGrammarSamplerRuntimeFailure(firstError)) {
                    if (state.config && state.config.debugMode) {
                        console.warn("[EasyLocalLLM][wllama] grammar sampling failed. Retrying without grammar.", firstError);
                    }
                    await runGenerationWithSampling(runtimeSamplingConfigNoGrammar);
                } else {
                    throw firstError;
                }
            }

            var remaining = sanitizer.flush();
            if (remaining) {
                content += remaining;
                if (ctx && typeof ctx.onChunk === "function") {
                    ctx.onChunk(remaining);
                }
            }
        } finally {
            if (ctx && ctx.signal && abortHandler) {
                ctx.signal.removeEventListener("abort", abortHandler);
            }
        }

        if (aborted) {
            throw normalizeAbortError();
        }

        return {
            content: content
        };
    }

    window.EasyLocalLLMLlamaBridge = {
        init: async function (config) {
            state.config = config || {};
            await ensureRuntime();
            state.initialized = true;
        },

        generate: async function (request, context) {
            if (!state.initialized) {
                await window.EasyLocalLLMLlamaBridge.init(state.config || {});
            }

            var req = request || {};
            var sessionId = req.sessionId || "default";
            state.sessions[sessionId] = true;

            return generateWithRuntime(state.runtime, req, context || {});
        },

        resetSession: function (sessionId) {
            if (!sessionId) {
                return;
            }

            delete state.sessions[sessionId];
        }
    };

    window.EasyLocalLLMRegisterLlamaRuntime = function (runtime) {
        if (!runtime || typeof runtime.init !== "function" || typeof runtime.generate !== "function") {
            throw new Error("runtime must implement init(config) and generate(request, context)");
        }

        window.EasyLocalLLMLlamaBridge = runtime;
    };
})();
