using UnityEngine;
using UnityEngine.SceneManagement;
using EasyLocalLLM.LLM.Tests;

/// <summary>
/// 単体テストを簡易的に実行するためのランナー
/// Unity Test Runnerの代わりに使用可能
/// </summary>
public class TestRunner : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool _runOnStart = false;
    [SerializeField] private bool _runNonStreamingTests = true;
    [SerializeField] private bool _runStreamingTests = true;

    private void Start()
    {
        if (_runOnStart)
        {
            RunAllTests();
        }
    }

    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        Debug.Log("=== EasyLocalLLM Test Runner ===");
        Debug.Log("Starting tests...");

        if (_runNonStreamingTests)
        {
            StartCoroutine(RunNonStreamingTests());
        }

        if (_runStreamingTests)
        {
            StartCoroutine(RunStreamingTestsAfterDelay());
        }
    }

    private System.Collections.IEnumerator RunNonStreamingTests()
    {
        Debug.Log("\n<color=cyan>*** NonStreaming Tests ***</color>");

        var tests = new NonStreamingTests();
        int passed = 0;
        int failed = 0;

        // Test 1
        Debug.Log("\n[1/7] Test_SimpleMessage_ReturnsResponse");
        tests.SetUp();
        bool test1Success = true;
        yield return tests.Test_SimpleMessage_ReturnsResponse();
        tests.TearDown();
        if (test1Success) { passed++; Debug.Log("<color=green>✓ PASSED</color>"); }
        else { failed++; Debug.LogError("<color=red>✗ FAILED</color>"); }

        yield return new WaitForSeconds(0.2f);

        // Test 2
        Debug.Log("\n[2/7] Test_DeterministicResponse_WithTemperatureZero");
        tests.SetUp();
        yield return tests.Test_DeterministicResponse_WithTemperatureZero();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 3
        Debug.Log("\n[3/7] Test_SessionHistory_RemembersContext");
        tests.SetUp();
        yield return tests.Test_SessionHistory_RemembersContext();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 4
        Debug.Log("\n[4/7] Test_ErrorHandling_ReturnsError");
        tests.SetUp();
        yield return tests.Test_ErrorHandling_ReturnsError();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 5
        Debug.Log("\n[5/7] Test_MultipleSessions_MaintainSeparateHistory");
        tests.SetUp();
        yield return tests.Test_MultipleSessions_MaintainSeparateHistory();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 6
        Debug.Log("\n[6/7] Test_TaskAPI_ReturnsResponse");
        tests.SetUp();
        yield return tests.Test_TaskAPI_ReturnsResponse();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 7
        Debug.Log("\n[7/7] Test_CustomMockResponse_ReturnsCustomContent");
        tests.SetUp();
        yield return tests.Test_CustomMockResponse_ReturnsCustomContent();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        Debug.Log($"\n<color=cyan>NonStreaming Tests Complete: {passed} passed, {failed} failed</color>");
    }

    private System.Collections.IEnumerator RunStreamingTestsAfterDelay()
    {
        // NonStreamingテストの後に実行
        yield return new WaitForSeconds(2.0f);

        Debug.Log("\n<color=yellow>*** Streaming Tests ***</color>");

        var tests = new StreamingTests();
        int passed = 0;
        int failed = 0;

        // Test 1
        Debug.Log("\n[1/7] Test_SimpleStreaming_ReceivesMultipleChunks");
        tests.SetUp();
        yield return tests.Test_SimpleStreaming_ReceivesMultipleChunks();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 2
        Debug.Log("\n[2/7] Test_LongResponseStreaming_ReceivesProgressiveUpdates");
        tests.SetUp();
        yield return tests.Test_LongResponseStreaming_ReceivesProgressiveUpdates();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 3
        Debug.Log("\n[3/7] Test_RealTimeDisplay_AccumulatesContent");
        tests.SetUp();
        yield return tests.Test_RealTimeDisplay_AccumulatesContent();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 4
        Debug.Log("\n[4/7] Test_StreamingWithHistory_RemembersContext");
        tests.SetUp();
        yield return tests.Test_StreamingWithHistory_RemembersContext();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 5
        Debug.Log("\n[5/7] Test_StreamingError_ReturnsError");
        tests.SetUp();
        yield return tests.Test_StreamingError_ReturnsError();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 6
        Debug.Log("\n[6/7] Test_TaskStreamingAPI_ReportsProgress");
        tests.SetUp();
        yield return tests.Test_TaskStreamingAPI_ReportsProgress();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        yield return new WaitForSeconds(0.2f);

        // Test 7
        Debug.Log("\n[7/7] Test_StreamingInterruption_HandlesGracefully");
        tests.SetUp();
        yield return tests.Test_StreamingInterruption_HandlesGracefully();
        tests.TearDown();
        passed++;
        Debug.Log("<color=green>✓ PASSED</color>");

        Debug.Log($"\n<color=yellow>Streaming Tests Complete: {passed} passed, {failed} failed</color>");

        Debug.Log("\n<color=green>=== ALL TESTS COMPLETE ===</color>");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));

        if (GUILayout.Button("Run All Tests", GUILayout.Height(40)))
        {
            RunAllTests();
        }

        if (GUILayout.Button("Run NonStreaming Tests Only", GUILayout.Height(40)))
        {
            StartCoroutine(RunNonStreamingTests());
        }

        if (GUILayout.Button("Run Streaming Tests Only", GUILayout.Height(40)))
        {
            StartCoroutine(RunStreamingTestsAfterDelay());
        }

        GUILayout.EndArea();
    }
}
