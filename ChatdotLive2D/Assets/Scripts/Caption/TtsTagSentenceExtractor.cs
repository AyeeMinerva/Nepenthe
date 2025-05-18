using System;

public class TtsTagSentenceExtractor
{
    private string _buffer = "";
    private bool _inTtsTag = false;
    private readonly string[] _sentenceEndPunctuation = { "。", "！", "？", ".", "!", "?", "\n" };
    private const string StartTag = "<tts>";
    private const string EndTag = "</tts>";

    public (string processedText, string newBuffer) ProcessTextChunk(string textChunk, string buffer, bool forceProcess = false)
    {
        _buffer = (buffer ?? "") + (textChunk ?? "");
        string processedText = "";

        while (true)
        {
            if (string.IsNullOrEmpty(_buffer)) break;

            var initialBuffer = _buffer;
            var initialTagState = _inTtsTag;

            if (!_inTtsTag)
            {
                int startIdx = _buffer.IndexOf(StartTag, StringComparison.OrdinalIgnoreCase);
                if (startIdx != -1)
                {
                    _buffer = _buffer.Substring(startIdx + StartTag.Length);
                    _inTtsTag = true;
                }
                else
                {
                    if (forceProcess) _buffer = "";
                    break;
                }
            }
            else
            {
                int endIdx = _buffer.IndexOf(EndTag, StringComparison.OrdinalIgnoreCase);
                if (endIdx != -1)
                {
                    string inTag = _buffer.Substring(0, endIdx);
                    string afterTag = _buffer.Substring(endIdx + EndTag.Length);

                    int lastPunct = FindLastSentenceEnd(inTag);
                    if (lastPunct != -1)
                    {
                        string sentences = inTag.Substring(0, lastPunct + 1).Trim();
                        string remainInTag = inTag.Substring(lastPunct + 1);
                        if (!string.IsNullOrEmpty(sentences))
                            processedText += (processedText.Length > 0 ? " " : "") + sentences;
                        _buffer = remainInTag + afterTag;
                    }
                    else
                    {
                        _buffer = afterTag;
                    }
                    _inTtsTag = false;
                }
                else
                {
                    int lastPunct = FindLastSentenceEnd(_buffer);
                    if (lastPunct != -1)
                    {
                        string sentences = _buffer.Substring(0, lastPunct + 1).Trim();
                        if (!string.IsNullOrEmpty(sentences))
                            processedText += (processedText.Length > 0 ? " " : "") + sentences;
                        _buffer = _buffer.Substring(lastPunct + 1);
                        break;
                    }
                    else
                    {
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

            if (_buffer == initialBuffer && _inTtsTag == initialTagState)
                break;
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