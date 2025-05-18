using UnityEngine;
using TMPro; // 引入TMP命名空间
using System.Collections;

public class TtsCaptionDisplayController : MonoBehaviour
{
    private TtsTagSentenceExtractor extractor = new TtsTagSentenceExtractor();
    private string buffer = "";

    [Header("字幕显示（TMP Text）")]
    public TMP_Text uiText; // 修改为TMP_Text类型

    [Header("字幕显示时长（秒）")]
    public float clearDelay = 2f;

    private Coroutine clearCoroutine;

    private bool subscribed = false;

    void OnEnable()
    {
        StartCoroutine(WaitAndSubscribe());
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (HttpServerManager.Instance == null || HttpServerManager.Instance.Server == null)
            yield return null;

        if (!subscribed)
        {
            Debug.Log("[TTS] 订阅 OnTextChunkReceived");
            HttpServerManager.Instance.Server.OnTextChunkReceived += OnTextChunkReceived;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribed && HttpServerManager.Instance != null && HttpServerManager.Instance.Server != null)
        {
            HttpServerManager.Instance.Server.OnTextChunkReceived -= OnTextChunkReceived;
            subscribed = false;
        }
    }

    private void OnTextChunkReceived(string chunk)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            var (processed, newBuffer) = extractor.ProcessTextChunk(chunk, buffer, false);
            buffer = newBuffer;
            if (!string.IsNullOrEmpty(processed))
            {
                ShowCaption(processed);
            }
        });
    }

    private void ShowCaption(string text)
    {
        Debug.Log("[TTS字幕] " + text);
        if (uiText != null)
            uiText.text = text;

        if (clearCoroutine != null)
            StopCoroutine(clearCoroutine);
        clearCoroutine = StartCoroutine(ClearAfterDelay());
    }

    private IEnumerator ClearAfterDelay()
    {
        yield return new WaitForSeconds(clearDelay);
        if (uiText != null)
            uiText.text = "";
    }

    void OnDestroy()
    {
        Unsubscribe();
        var (processed, _) = extractor.ProcessTextChunk("", buffer, true);
        if (!string.IsNullOrEmpty(processed))
        {
            ShowCaption(processed);
        }
        if (clearCoroutine != null)
            StopCoroutine(clearCoroutine);
    }
}