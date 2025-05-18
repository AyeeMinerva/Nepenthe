using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine; // For Debug and JsonUtility
using ChatdotLive2D.Data; // Adjust namespace - EmotionMapping might still be used elsewhere or for future logic
using ChatdotLive2D.HttpServer.DataModels; // Now includes TextChunkCommand

namespace ChatdotLive2D.HttpServer
{
    /// <summary>
    /// 读取请求体、解析 JSON (期待 {'chunk': '...'})、发送响应。
    /// 处理文本块并发出结果信号。
    /// </summary>
    public class HttpRequestProcessor
    {
        private readonly HttpListenerContext context;
        // EmotionMapping might still be needed for subsequent logic based on the text,
        // but is not directly used for parsing the incoming format anymore.
        // 如果后续需要根据文本进行情绪分析或映射，可能还需要 EmotionMapping，
        // 但不再用于直接解析传入格式。
        private readonly EmotionMapping emotionMapping; // Keep for potential future use with chunk text

        /// <summary>
        /// Event to signal the result of processing this request: (success, textChunk, errorMessage)
        /// 处理此请求的结果信号：(是否成功, 文本块内容, 错误消息)
        /// </summary>
        public event Action<bool, string, string> OnCommandProcessed; // Renamed event and changed signature

        public HttpRequestProcessor(HttpListenerContext context, EmotionMapping emotionMapping)
        {
            this.context = context;
            this.emotionMapping = emotionMapping; // Still keep the mapping reference
        }

        public void HandleRequest()
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            Debug.Log($"Processor: Received request: {request.HttpMethod} {request.RawUrl}");

            // (Recommended) Add basic CORS headers
            // (推荐) 添加基本的 CORS 头
            response.Headers.Set("Access-Control-Allow-Origin", "*");

            if (request.HttpMethod != "POST")
            {
                Debug.LogWarning($"Processor: Received non-POST method: {request.HttpMethod}. Sending error response.");
                SendErrorResponse(response, "Only POST method is supported", HttpStatusCode.MethodNotAllowed);
                // For unsupported methods, we don't consider it a command needing main thread processing
                // 对于不支持的方法，我们不认为这是需要主线程处理的命令
                return;
            }

            string requestBody = "";
            TextChunkCommand command = null; // Use the new command type
            try
            {
                requestBody = ReadRequestBody(request);
                // Debug.Log($"Processor: Request Body: {requestBody}"); // Optional debug

                // *** MODIFIED: Parse using the new TextChunkCommand model ***
                command = JsonUtility.FromJson<TextChunkCommand>(requestBody);

                // *** MODIFIED: Validate the new command structure ***
                if (command == null || string.IsNullOrEmpty(command.chunk))
                {
                    Debug.LogWarning("Processor: Failed to parse JSON or chunk is empty. Sending error response.");
                    SendErrorResponse(response, "Invalid JSON format or missing 'chunk' field.");
                    Debug.Log("Processor: Invoking OnCommandProcessed (Error: Invalid JSON/Chunk).");
                    // *** MODIFIED: Invoke new event with text chunk (or null) ***
                    OnCommandProcessed?.Invoke(false, command?.chunk, "Invalid JSON format or missing 'chunk' field."); // Pass chunk if command is not null but chunk is empty
                    return;
                }

                // *** MODIFIED: The old emotion mapping logic is removed here ***
                // We successfully parsed the text chunk. Now we signal success and pass the chunk.
                Debug.Log($"Processor: Successfully parsed chunk: '{command.chunk}'. Sending success response.");
                SendSuccessResponse(response);
                Debug.Log($"Processor: Invoking OnCommandProcessed (Success) with chunk: '{command.chunk}'.");
                // *** MODIFIED: Invoke new event with the parsed text chunk ***
                OnCommandProcessed?.Invoke(true, command.chunk, null); // Success, pass the text chunk

                // *** TODO: Add logic here to process the 'command.chunk' text ***
                // What should happen based on the text?
                // - Perform sentiment analysis?
                // - Look for keywords to trigger specific animations using the emotionMapping?
                // - Just pass the text along for display?
                // This part depends on your application's requirements.
                // This event OnCommandProcessed is designed to pass the chunk
                // so the main thread can handle it.

            }
            catch (Exception e) // Catch errors during body reading or JSON parsing
            {
                Debug.LogError($"Processor: Error processing request body: {e.Message}. Body: '{requestBody}'. Sending error response.");
                SendErrorResponse(response, "Error processing request.");
                Debug.Log("Processor: Invoking OnCommandProcessed (Error: Exception during processing).");
                // *** MODIFIED: Invoke new event with text chunk (if available) and error message ***
                OnCommandProcessed?.Invoke(false, command?.chunk, $"Error processing request: {e.Message}"); // Pass chunk if parsing partially succeeded
                // Added return here to ensure finally block is reached after catch
                return;
            }
            finally
            {
                // Always close the response stream
                // 始终关闭响应流
                response?.Close();
                Debug.Log("Processor: Response stream closed.");
            }
        }

        private string ReadRequestBody(HttpListenerRequest request)
        {
            using (var body = request.InputStream)
            // Use UTF8 if encoding is null
            using (var reader = new StreamReader(body, request.ContentEncoding ?? Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private void SendSuccessResponse(HttpListenerResponse response)
        {
            SendResponse(response, "{\"status\":\"success\"}", HttpStatusCode.OK);
        }

        private void SendErrorResponse(HttpListenerResponse response, string errorMessage, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            // Simple JSON error response
            // 简单的 JSON 错误响应
            // Basic escaping for double quotes
            string jsonError = $"{{\"status\":\"error\", \"message\":\"{errorMessage.Replace("\"", "\\\"")}\"}}";
            SendResponse(response, jsonError, statusCode);
        }

        private void SendResponse(HttpListenerResponse response, string responseString, HttpStatusCode statusCode)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.StatusCode = (int)statusCode;
                response.ContentType = "application/json"; // Set response type
                response.ContentEncoding = Encoding.UTF8;
                using (var output = response.OutputStream)
                {
                    output.Write(buffer, 0, buffer.Length);
                }
                // Debug.Log($"Processor: Response sent with status {statusCode}. Body: {responseString}"); // Optional debug
            }
            catch (Exception e)
            {
                Debug.LogError($"Processor: Failed to send response: {e.Message}");
            }
            // Note: Do not close the response stream here. It's closed in the finally block of HandleRequest.
            // 注意：不要在这里关闭响应流。它在 HandleRequest 的 finally 块中关闭。
        }
    }
}