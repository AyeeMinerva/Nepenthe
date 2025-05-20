using UnityEngine;
using System.Collections;

public class Live2dMotionFromChunkController : MonoBehaviour
{
    public Live2DMotionController live2dController;

    private string buffer = "";
    private bool inLive2dTag = false;
    private const string StartTag = "<live2d>";
    private const string EndTag = "</live2d>";
    private const string OutputEndTag = "[/Assistant Output]";

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
        while (HttpServerManager.Instance == null || HttpServerManager.Instance.Server == null)
            yield return null;

        Debug.Log("[Live2D] 订阅 OnTextChunkReceived");
        HttpServerManager.Instance.Server.OnTextChunkReceived += OnTextChunkReceived;
    }

    private void OnTextChunkReceived(string chunk)
    {
        buffer += chunk ?? "";

        while (true)
        {
            // 1. 优先处理[/Assistant Output]，清空到该标签为止
            int outputEndIdx = buffer.IndexOf(OutputEndTag, System.StringComparison.OrdinalIgnoreCase);
            if (outputEndIdx != -1)
            {
                buffer = buffer.Substring(outputEndIdx + OutputEndTag.Length);
                inLive2dTag = false;
                continue;
            }

            if (!inLive2dTag)
            {
                // 2. 查找<live2d>标签
                int tagStart = buffer.IndexOf(StartTag, System.StringComparison.OrdinalIgnoreCase);
                if (tagStart == -1)
                    break;
                if (buffer.Length < tagStart + StartTag.Length)
                    break; // 标签被分割，等待更多chunk
                buffer = buffer.Substring(tagStart + StartTag.Length);
                inLive2dTag = true;
                continue;
            }
            else
            {
                // 3. 查找</live2d>标签
                int tagEnd = buffer.IndexOf(EndTag, System.StringComparison.OrdinalIgnoreCase);
                if (tagEnd == -1)
                    break; // 标签未闭合，等待更多chunk
                string motionName = buffer.Substring(0, tagEnd).Trim();
                if (!string.IsNullOrEmpty(motionName) && live2dController != null)
                {
                    MainThreadDispatcher.Enqueue(() => live2dController.PlayMotion(motionName));
                }
                buffer = buffer.Substring(tagEnd + EndTag.Length);
                inLive2dTag = false;
                continue;
            }
        }
    }
}