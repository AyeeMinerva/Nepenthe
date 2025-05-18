using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections;

public class Live2dMotionFromChunkController : MonoBehaviour
{
    public Live2DMotionController live2dController;
    private static readonly Regex live2dRegex = new Regex(@"<live2d>(.*?)</live2d>", RegexOptions.IgnoreCase);

    void OnEnable()
    {
        Debug.Log("[Live2D] Live2dMotionFromChunkController OnEnable");
        StartCoroutine(WaitAndSubscribe());
    }

    void OnDisable()
    {
        Debug.Log("[Live2D] Live2dMotionFromChunkController OnDisable");
        if (HttpServerManager.Instance != null && HttpServerManager.Instance.Server != null)
            HttpServerManager.Instance.Server.OnTextChunkReceived -= OnTextChunkReceived;
    }

    private IEnumerator WaitAndSubscribe()
    {
        // 等待Server初始化
        while (HttpServerManager.Instance == null || HttpServerManager.Instance.Server == null)
            yield return null;

        Debug.Log("[Live2D] 订阅 OnTextChunkReceived");
        HttpServerManager.Instance.Server.OnTextChunkReceived += OnTextChunkReceived;
    }

    private void OnTextChunkReceived(string chunk)
    {
        Debug.Log("[Live2D] 收到chunk: " + chunk);
        var match = live2dRegex.Match(chunk);
        if (match.Success)
        {
            string motionName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(motionName) && live2dController != null)
            {
                MainThreadDispatcher.Enqueue(() => live2dController.PlayMotion(motionName));
            }
        }
    }
}