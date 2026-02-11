using System.Collections;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// Coroutine runner for Task bridge
    /// </summary>
    internal sealed class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        /// <summary>
        /// Ensure CoroutineRunner instance exists
        /// </summary>
        private static CoroutineRunner EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("EasyLocalLLM.CoroutineRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CoroutineRunner>();
            return _instance;
        }

        /// <summary>
        /// Run a coroutine
        /// </summary>
        public static Coroutine Run(IEnumerator routine)
        {
            return EnsureInstance().StartCoroutine(routine);
        }
    }
}