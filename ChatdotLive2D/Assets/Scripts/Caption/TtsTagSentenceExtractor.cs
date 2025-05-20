using System;

public class TtsTagSentenceExtractor
{
    private string _buffer = "";
    private bool _inTtsTag = false;
    private readonly string[] _sentenceEndPunctuation = { "。", "！", "？", ".", "!", "?", "\n" };
    private const string StartTag = "<tts>";
    private const string EndTag = "</tts>";
    private const string OutputEndTag = "[/Assistant Output]";

    public (string processedText, string newBuffer) ProcessTextChunk(string textChunk, string buffer, bool forceProcess = false)
    {
        _buffer = (buffer ?? "") + (textChunk ?? "");
        string processedText = "";

        while (true)
        {
            // 优先处理[/Assistant Output]，清空到该标签为止
            int outputEndIdx = _buffer.IndexOf(OutputEndTag, StringComparison.OrdinalIgnoreCase);
            if (outputEndIdx != -1)
            {
                // 清空到标签为止，保留后续内容
                _buffer = _buffer.Substring(outputEndIdx + OutputEndTag.Length);
                _inTtsTag = false;
                continue;
            }

            if (!_inTtsTag)
            {
                // 查找<tts>标签
                int ttsStartIdx = _buffer.IndexOf(StartTag, StringComparison.OrdinalIgnoreCase);
                if (ttsStartIdx == -1)
                {
                    // 没有<tts>，不处理
                    if (forceProcess) _buffer = "";
                    break;
                }
                // <tts>标签可能被分割，等标签完整再进入
                if (_buffer.Length < ttsStartIdx + StartTag.Length)
                    break;
                // 删除<tts>及其前内容，进入tts状态
                _buffer = _buffer.Substring(ttsStartIdx + StartTag.Length);
                _inTtsTag = true;
                continue;
            }
            else
            {
                // tts状态下，查找</tts>标签
                int ttsEndIdx = _buffer.IndexOf(EndTag, StringComparison.OrdinalIgnoreCase);
                if (ttsEndIdx != -1)
                {
                    // 处理标签内内容
                    string inTag = _buffer.Substring(0, ttsEndIdx);
                    // 输出所有完整句子
                    int lastPunct = FindLastSentenceEnd(inTag);
                    if (lastPunct != -1)
                    {
                        string sentences = inTag.Substring(0, lastPunct + 1).Trim();
                        if (!string.IsNullOrEmpty(sentences))
                            processedText += (processedText.Length > 0 ? " " : "") + sentences;
                        // 剩余未成句内容
                        inTag = inTag.Substring(lastPunct + 1);
                    }
                    // 处理剩余未成句内容（如果forceProcess或遇到</tts>时也要输出）
                    string remain = inTag.Trim();
                    if (!string.IsNullOrEmpty(remain))
                        processedText += (processedText.Length > 0 ? " " : "") + remain;
                    // 跳过</tts>标签
                    _buffer = _buffer.Substring(ttsEndIdx + EndTag.Length);
                    _inTtsTag = false;
                    continue;
                }
                else
                {
                    // 没有</tts>，实时输出句子
                    int lastPunct = FindLastSentenceEnd(_buffer);
                    if (lastPunct != -1)
                    {
                        string sentences = _buffer.Substring(0, lastPunct + 1).Trim();
                        if (!string.IsNullOrEmpty(sentences))
                            processedText += (processedText.Length > 0 ? " " : "") + sentences;
                        _buffer = _buffer.Substring(lastPunct + 1);
                        continue;
                    }
                    else
                    {
                        // 没有句末标点，等待更多内容
                        if (forceProcess)
                        {
                            string sentences = _buffer.Trim();
                            if (!string.IsNullOrEmpty(sentences))
                                processedText += (processedText.Length > 0 ? " " : "") + sentences;
                            _buffer = "";
                        }
                        break;
                    }
                }
            }
        }

        return (processedText, _buffer);
    }

    private int FindLastSentenceEnd(string text)
    {
        int last = -1;
        foreach (var p in _sentenceEndPunctuation)
        {
            int idx = text.LastIndexOf(p, StringComparison.Ordinal);
            if (idx > last) last = idx;
        }
        return last;
    }
}