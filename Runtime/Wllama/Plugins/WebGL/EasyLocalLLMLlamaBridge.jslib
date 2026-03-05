mergeInto(LibraryManager.library, {
  $EasyLocalLLM_BridgeState: {
    activeControllers: {},
    callback: {
      gameObjectName: null,
      methodName: null
    },
    runtimeScriptLoadPromise: null
  },

  $EasyLocalLLM_ToString: function (ptr) {
    return UTF8ToString(ptr);
  },

  $EasyLocalLLM_GetBridge: function () {
    return typeof window !== "undefined" ? window.EasyLocalLLMLlamaBridge : null;
  },

  $EasyLocalLLM_GetRuntimeScriptUrl: function () {
    if (typeof window !== "undefined" && window.EasyLocalLLMRuntimeScriptUrl) {
      return window.EasyLocalLLMRuntimeScriptUrl;
    }

    var streamingAssetsUrl = "StreamingAssets";
    if (typeof Module !== "undefined" && Module && Module.streamingAssetsUrl) {
      streamingAssetsUrl = Module.streamingAssetsUrl;
    }

    return streamingAssetsUrl.replace(/\/$/, "") + "/EasyLocalLLM/WebGL/EasyLocalLLMLlamaBridgeRuntime.js";
  },

  $EasyLocalLLM_LoadScript: function (url) {
    return new Promise(function (resolve, reject) {
      if (typeof document === "undefined") {
        reject(new Error("document is not available for runtime script loading."));
        return;
      }

      var existing = document.querySelector('script[data-easy-local-llm-runtime="1"]');
      if (existing) {
        existing.addEventListener("load", function () { resolve(); }, { once: true });
        existing.addEventListener("error", function () { reject(new Error("Failed to load runtime script: " + url)); }, { once: true });
        return;
      }

      var script = document.createElement("script");
      script.async = true;
      script.src = url;
      script.setAttribute("data-easy-local-llm-runtime", "1");
      script.onload = function () { resolve(); };
      script.onerror = function () { reject(new Error("Failed to load runtime script: " + url)); };
      document.head.appendChild(script);
    });
  },

  $EasyLocalLLM_EnsureBridgeRuntime__deps: ["$EasyLocalLLM_BridgeState", "$EasyLocalLLM_GetBridge", "$EasyLocalLLM_GetRuntimeScriptUrl", "$EasyLocalLLM_LoadScript"],
  $EasyLocalLLM_EnsureBridgeRuntime: function () {
    var bridge = EasyLocalLLM_GetBridge();
    if (bridge && typeof bridge.init === "function") {
      return Promise.resolve(bridge);
    }

    if (!EasyLocalLLM_BridgeState.runtimeScriptLoadPromise) {
      var runtimeScriptUrl = EasyLocalLLM_GetRuntimeScriptUrl();
      EasyLocalLLM_BridgeState.runtimeScriptLoadPromise = EasyLocalLLM_LoadScript(runtimeScriptUrl)
        .then(function () {
          var loadedBridge = EasyLocalLLM_GetBridge();
          if (!loadedBridge || typeof loadedBridge.init !== "function") {
            throw new Error("window.EasyLocalLLMLlamaBridge.init is not available after loading runtime script.");
          }
          return loadedBridge;
        })
        .catch(function (error) {
          EasyLocalLLM_BridgeState.runtimeScriptLoadPromise = null;
          throw error;
        });
    }

    return EasyLocalLLM_BridgeState.runtimeScriptLoadPromise;
  },

  $EasyLocalLLM_Dispatch: function (eventPayload) {
    var callback = EasyLocalLLM_BridgeState.callback;
    if (!callback.gameObjectName || !callback.methodName) {
      return;
    }

    try {
      var payload = JSON.stringify(eventPayload);
      if (typeof SendMessage === "function") {
        SendMessage(callback.gameObjectName, callback.methodName, payload);
        return;
      }

      if (typeof unityInstance !== "undefined" && unityInstance && typeof unityInstance.SendMessage === "function") {
        unityInstance.SendMessage(callback.gameObjectName, callback.methodName, payload);
        return;
      }

      if (typeof Module !== "undefined" && Module && typeof Module.SendMessage === "function") {
        Module.SendMessage(callback.gameObjectName, callback.methodName, payload);
        return;
      }

      console.error("[EasyLocalLLM] Unity SendMessage is not available.");
    } catch (error) {
      console.error("[EasyLocalLLM] Failed to dispatch bridge event", error);
    }
  },

  EasyLocalLLM_Llama_Init__deps: ["$EasyLocalLLM_BridgeState", "$EasyLocalLLM_ToString", "$EasyLocalLLM_EnsureBridgeRuntime", "$EasyLocalLLM_Dispatch"],
  EasyLocalLLM_Llama_Init: function (gameObjectNamePtr, callbackMethodNamePtr, configJsonPtr) {
    EasyLocalLLM_BridgeState.callback.gameObjectName = EasyLocalLLM_ToString(gameObjectNamePtr);
    EasyLocalLLM_BridgeState.callback.methodName = EasyLocalLLM_ToString(callbackMethodNamePtr);

    var configJson = EasyLocalLLM_ToString(configJsonPtr);
    var config = {};

    try {
      config = configJson ? JSON.parse(configJson) : {};
    } catch (error) {
      EasyLocalLLM_Dispatch({
        type: "init",
        success: false,
        error: "Invalid init config JSON: " + error.message
      });
      return;
    }

    Promise.resolve(EasyLocalLLM_EnsureBridgeRuntime())
      .then(function (bridge) {
        return bridge.init(config);
      })
      .then(function () {
        EasyLocalLLM_Dispatch({ type: "init", success: true });
      })
      .catch(function (error) {
        EasyLocalLLM_Dispatch({
          type: "init",
          success: false,
          error: error && error.message ? error.message : String(error)
        });
      });
  },

  EasyLocalLLM_Llama_Generate__deps: ["$EasyLocalLLM_BridgeState", "$EasyLocalLLM_ToString", "$EasyLocalLLM_GetBridge", "$EasyLocalLLM_Dispatch"],
  EasyLocalLLM_Llama_Generate: function (requestJsonPtr) {
    var requestJson = EasyLocalLLM_ToString(requestJsonPtr);
    var request;

    try {
      request = JSON.parse(requestJson);
    } catch (error) {
      EasyLocalLLM_Dispatch({
        type: "error",
        requestId: "",
        error: "Invalid request JSON: " + error.message
      });
      return;
    }

    var requestId = request && request.requestId ? request.requestId : "";
    var bridge = EasyLocalLLM_GetBridge();
    if (!bridge || typeof bridge.generate !== "function") {
      EasyLocalLLM_Dispatch({
        type: "error",
        requestId: requestId,
        error: "window.EasyLocalLLMLlamaBridge.generate is not available."
      });
      return;
    }

    var controller = typeof AbortController !== "undefined" ? new AbortController() : null;
    if (requestId && controller) {
      EasyLocalLLM_BridgeState.activeControllers[requestId] = controller;
    }

    var generateContext = {
      signal: controller ? controller.signal : undefined,
      onChunk: function (chunkText) {
        EasyLocalLLM_Dispatch({
          type: "chunk",
          requestId: requestId,
          content: chunkText == null ? "" : String(chunkText)
        });
      }
    };

    Promise.resolve(bridge.generate(request, generateContext))
      .then(function (result) {
        if (requestId && EasyLocalLLM_BridgeState.activeControllers[requestId]) {
          delete EasyLocalLLM_BridgeState.activeControllers[requestId];
        }

        var donePayload = {
          type: "done",
          requestId: requestId
        };

        if (result && typeof result === "object") {
          donePayload.raw = result;
          if (typeof result.content === "string") {
            donePayload.content = result.content;
          }
        } else if (result != null) {
          donePayload.content = String(result);
        }

        EasyLocalLLM_Dispatch(donePayload);
      })
      .catch(function (error) {
        if (requestId && EasyLocalLLM_BridgeState.activeControllers[requestId]) {
          delete EasyLocalLLM_BridgeState.activeControllers[requestId];
        }

        EasyLocalLLM_Dispatch({
          type: "error",
          requestId: requestId,
          error: error && error.message ? error.message : String(error)
        });
      });
  },

  EasyLocalLLM_Llama_Abort__deps: ["$EasyLocalLLM_BridgeState", "$EasyLocalLLM_ToString"],
  EasyLocalLLM_Llama_Abort: function (requestIdPtr) {
    var requestId = EasyLocalLLM_ToString(requestIdPtr);
    var controller = EasyLocalLLM_BridgeState.activeControllers[requestId];
    if (!controller) {
      return;
    }

    try {
      controller.abort();
    } finally {
      delete EasyLocalLLM_BridgeState.activeControllers[requestId];
    }
  },

  EasyLocalLLM_Llama_ResetSession__deps: ["$EasyLocalLLM_ToString", "$EasyLocalLLM_GetBridge"],
  EasyLocalLLM_Llama_ResetSession: function (sessionIdPtr) {
    var sessionId = EasyLocalLLM_ToString(sessionIdPtr);
    var bridge = EasyLocalLLM_GetBridge();

    if (!bridge || typeof bridge.resetSession !== "function") {
      return;
    }

    try {
      bridge.resetSession(sessionId);
    } catch (error) {
      console.warn("[EasyLocalLLM] resetSession failed", error);
    }
  }
});
