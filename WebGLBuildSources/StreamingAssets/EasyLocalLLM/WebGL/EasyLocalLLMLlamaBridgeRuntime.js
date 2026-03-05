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
        var messages = req.messages || [];
        var prompt = buildPrompt(messages);
        var chatMessages = buildChatMessages(messages);
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

        try {
            if (typeof runtime.createChatCompletion === "function") {
                var stream = await runtime.createChatCompletion(chatMessages, {
                    nPredict: sampling.nPredict,
                    sampling: {
                        temp: sampling.temperature,
                        top_p: sampling.topP,
                        top_k: sampling.topK,
                        min_p: sampling.minP,
                        seed: sampling.seed
                    },
                    stream: true
                });

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

                await runtime.createCompletion(prompt, {
                    nPredict: sampling.nPredict,
                    sampling: {
                        temp: sampling.temperature,
                        top_p: sampling.topP,
                        top_k: sampling.topK,
                        min_p: sampling.minP,
                        seed: sampling.seed
                    },
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
                });
            } else if (typeof runtime.completion === "function") {
                var response = await runtime.completion(prompt, {
                    n_predict: sampling.nPredict,
                    temperature: sampling.temperature,
                    top_p: sampling.topP,
                    top_k: sampling.topK,
                    min_p: sampling.minP,
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
                });

                if (!content && response && typeof response.content === "string") {
                    content = sanitizer.push(response.content) + sanitizer.flush();
                    if (ctx && typeof ctx.onChunk === "function") {
                        ctx.onChunk(content);
                    }
                }
            } else {
                throw new Error("Unsupported Wllama API. Expected createCompletion or completion.");
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
