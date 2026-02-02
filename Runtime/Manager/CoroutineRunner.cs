using System.Collections;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// Task ブリッジ用のコルーチンランナー
    /// </summary>
    internal sealed class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

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

        public static Coroutine Run(IEnumerator routine)
        {
            return EnsureInstance().StartCoroutine(routine);
        }
    }
}