// File: ChatdotLive2D/HttpServer/DataModels/TextChunkCommand.cs
using System; // For [Serializable]

namespace ChatdotLive2D.HttpServer.DataModels
{
    [Serializable]
    public class TextChunkCommand
    {
        public string chunk;
    }
}