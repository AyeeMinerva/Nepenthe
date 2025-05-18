using System;
using System.Collections;
using System.Net;
using UnityEngine; // 需要 Debug, Coroutine 支持, 和 WaitForSeconds
using ChatdotLive2D.Data; // 需要 EmotionMapping (尽管直接使用它的地方变了)
using ChatdotLive2D.Utilities; // 需要 WaitAsyncResult
// 如果 HttpRequestProcessor 需要这些模型，可能需要引用数据模型命名空间
using ChatdotLive2D.HttpServer.DataModels; // 需要引用数据模型命名空间来使用 TextChunkCommand

namespace ChatdotLive2D.HttpServer
{
    /// <summary>
    /// Manages HttpListener lifecycle and dispatches requests to HttpRequestProcessor.
    /// Receives text chunks from processors and signals to the main thread.
    /// 管理 HttpListener 生命周期，并将请求分派给 HttpRequestProcessor 处理。
    /// 从处理器接收文本块并信号通知主线程。
    /// </summary>
    public class HttpServer
    {
        private HttpListener listener;
        private bool isRunning = false;
        private readonly int port;
        // EmotionMapping is passed to HttpRequestProcessor, but HttpServer itself
        // no longer uses it directly in the event handler.
        // EmotionMapping 被传递给 HttpRequestProcessor，但 HttpServer 自己
        // 在事件处理器中不再直接使用它。
        private readonly EmotionMapping emotionMapping;

        /// <summary>
        /// Event triggered when a text chunk command is successfully processed.
        /// Signals the main thread with the received text chunk.
        /// 当文本块命令成功处理后触发。
        /// 用接收到的文本块通知主线程。
        /// </summary>
        // *** MODIFIED: Renamed event and changed signature to pass string (text chunk) ***
        public event Action<string> OnTextChunkReceived;


        public HttpServer(int port, EmotionMapping emotionMapping)
        {
            this.port = port;
            this.emotionMapping = emotionMapping;
        }

        /// <summary>
        /// Starts the HttpListener and enters the request listening loop coroutine.
        /// Must be started via a MonoBehaviour's StartCoroutine.
        /// 启动 HttpListener 并进入请求监听循环的协程。
        /// 需要通过 MonoBehaviour 的 StartCoroutine 启动。
        /// </summary>
        public IEnumerator StartServerCoroutine()
        {
            bool startupFailed = false;

            // --- 服务器启动 ---
            Debug.Log($"尝试在端口 {port} 启动 HTTP 服务器...");
            try
            {
                listener = new HttpListener();
                // Use '+' to listen on all available IPs, not just localhost
                // 使用 '+' 允许监听所有可用 IP，而不仅仅是 localhost
                listener.Prefixes.Add($"http://+:{port}/");
                listener.Start();
                isRunning = true;
                Debug.Log($"HTTP 服务器在端口 {port} 启动成功，开始监听请求。");
            }
            catch (Exception e)
            {
                Debug.LogError($"启动 HTTP Listener 失败，端口 {port}: {e.Message}");
                isRunning = false;
                startupFailed = true;
            }

            if (startupFailed)
            {
                Debug.LogError("HTTP 服务器启动失败，退出协程。");
                yield break;
            }

            // --- 服务器监听循环 ---
            Debug.Log("HTTP 服务器监听循环开始。");
            while (isRunning)
            {
                IAsyncResult result = null;
                bool errorDuringGetContext = false;

                try
                {
                    // Asynchronously begin waiting for an incoming request.
                    // When a request arrives, the HandleHttpRequest callback is invoked on a thread pool thread.
                    // 异步开始等待传入请求。
                    // 请求到达后，HandleHttpRequest 回调会在线程池线程上被调用。
                    result = listener.BeginGetContext(HandleHttpRequest, listener);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was closed while BeginGetContext was pending, normal loop exit
                    // Listener 在 BeginGetContext 挂起时被关闭，正常退出循环
                    Debug.Log("Listener 在 BeginGetContext 期间被释放，正在正常退出服务器循环。");
                    isRunning = false;
                    break;
                }
                catch (Exception e) // Catch other exceptions from BeginGetContext
                {
                    Debug.LogError($"listener.BeginGetContext 期间发生错误: {e.Message}");
                    errorDuringGetContext = true;
                }

                if (errorDuringGetContext)
                {
                    // If an error occurred, wait a short time before trying to get the next context
                    // 如果发生错误，等待一小段时间再尝试获取下一个上下文
                    Debug.Log("由于上一个错误，等待 1 秒后再次尝试 BeginGetContext。");
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // If BeginGetContext succeeded and returned a result, yield until the operation completes.
                // This yields control of the coroutine back to the Unity main thread until the request context is fully available.
                // 如果 BeginGetContext 成功并返回结果，等待操作完成。
                // 这会将协程的执行权交还给 Unity 主线程，直到请求上下文完全可用。
                if (result != null)
                {
                    // The coroutine will pause here until the async operation (getting context) finishes
                    // 协程会在此处暂停，直到异步操作 (获取上下文) 完成
                    yield return new ChatdotLive2D.Utilities.WaitAsyncResult(result);
                    // Note: The HandleHttpRequest callback will be invoked when the system is ready with the context,
                    // potentially before the WaitAsyncResult completes, but this yield ensures
                    // the loop doesn't immediately start waiting for the next context.
                    // 注意：HandleHttpRequest 回调会在系统准备好上下文时调用，
                    // 可能在 WaitAsyncResult 完成之前，但此处的 yield 确保
                    // 循环不会立即开始等待下一个上下文。
                }
                else
                {
                    // As a safeguard, if no exception was thrown but result is null (unlikely), wait a frame.
                    // 如果没有抛出异常但 result 为 null (不太可能)，作为 safeguard 等待一帧。
                    Debug.LogWarning("listener.BeginGetContext 返回 null 但未捕获到明确异常。等待一帧。");
                    yield return null;
                }
            }

            // --- Cleanup after loop exit ---
            // --- 循环退出后的清理 ---
            Debug.Log("HTTP 服务器循环结束，正在清理 listener。");
            // If the loop exited due to ObjectDisposedException, the listener should already be closed.
            // This ensures cleanup in other exit scenarios or if closing failed in catch.
            // 如果循环是因 ObjectDisposedException 退出，listener 应该已关闭。
            // 此处确保在其他退出场景或 catch 中关闭失败时进行清理。
            if (listener != null && listener.IsListening)
            {
                try
                {
                    // Stop receiving new requests and close existing connections/streams
                    // 停止接收新连接并关闭现有连接/流
                    listener.Stop(); // Stop receiving new connections
                    Debug.Log("HTTP 服务器 listener 已停止。");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"listener.Stop 期间发生异常: {e.Message}");
                }

                try
                {
                    listener.Close(); // Release resources
                    Debug.Log("HTTP 服务器 listener 已关闭。");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"listener.Close 期间发生异常: {e.Message}");
                }
            }
            listener = null; // Dereference listener

            Debug.Log("HTTP 服务器协程已结束。");
        }

        /// <summary>
        /// Called by HttpListener on a thread pool thread when a request context is available.
        /// 由 HttpListener 在线程池线程上调用，当请求上下文可用时执行。
        /// </summary>
        /// <param name="result">The async operation result containing the context.</param>
        private void HandleHttpRequest(IAsyncResult result)
        {
            // Ensure listener is still active and listening before processing
            // 在处理前确保 listener 仍然活跃并监听
            HttpListener listener = (HttpListener)result.AsyncState;
            if (!isRunning || listener == null || !listener.IsListening)
            {
                Debug.LogWarning("HandleHttpRequest 被调用，但服务器未运行、listener 为 null 或未监听。");
                // Attempt to get context and close response to avoid resource leaks, but handle EndGetContext potentially throwing.
                // 尝试获取上下文并关闭响应，避免资源泄漏，但需小心 EndGetContext 可能抛异常。
                try
                {
                    var contextWhenDisposed = listener?.EndGetContext(result);
                    contextWhenDisposed?.Response?.Close(); // Attempt to close response
                }
                catch { /* Ignore exceptions during cleanup attempt */ }

                return;
            }

            HttpListenerContext context = null;
            try
            {
                // Complete the async operation and get the context
                // 完成异步操作并获取上下文
                context = listener.EndGetContext(result);
            }
            catch (ObjectDisposedException)
            {
                // listener was closed after BeginGetContext but before EndGetContext, handled by loop exit logic.
                // listener 在 BeginGetContext 后但在 EndGetContext 前关闭，已由循环退出逻辑处理。
                Debug.Log("listener 在回调的 EndGetContext 期间被释放。");
                return;
            }
            catch (Exception e)
            {
                // Handle other potential errors getting context
                // 处理获取上下文的其他潜在错误
                Debug.LogError($"在回调中获取 HTTP 上下文错误: {e.Message}");
                // Cannot process request without context, just return.
                // 没有上下文无法处理请求，直接返回。
                return;
            }

            // Create a processor for this specific request context
            // 为此特定请求上下文创建一个处理器
            var requestProcessor = new HttpRequestProcessor(context, emotionMapping);

            // Subscribe to the processor's event to get the processing result
            // This callback occurs on the thread pool thread where HandleHttpRequest resides.
            // 订阅处理器的事件，获取处理结果
            // 此回调发生在 HandleHttpRequest 所在的线程池线程上。
            // *** MODIFIED: Subscribing to the new event name and updated lambda parameters ***
            requestProcessor.OnCommandProcessed += (success, textChunk, errorMessage) =>
            {
                if (success)
                {
                    // If the request was processed successfully (JSON parsed, chunk extracted),
                    // trigger our own event to notify the main thread controller to queue the command.
                    // 如果请求成功处理 (JSON 解析，文本块提取)，
                    // 触发我们自己的事件，通知主线程控制器排队执行命令。
                    Debug.Log($"HttpServer: Command processed successfully. Notifying main thread with chunk: '{textChunk}'"); // Added log
                    // *** MODIFIED: Invoke the new event, passing the text chunk ***
                    OnTextChunkReceived?.Invoke(textChunk);
                    // Note: The logic to map the textChunk to an animation input (int)
                    // should now happen in the main thread subscriber of OnTextChunkReceived.
                    // 注意：将 textChunk 映射到动画输入值 (int) 的逻辑
                    // 现在应该在 OnTextChunkReceived 事件的主线程订阅者中实现。
                }
                else
                {
                    // Optional: Log or handle errors from the processor if needed in HttpServer
                    // 可选：如果 HttpServer 需要处理处理器中的错误，可以在这里添加日志或处理逻辑
                    Debug.LogWarning($"HttpServer: RequestProcessor reported failure. Message: {errorMessage}"); // Added log
                }
                // Note: HttpRequestProcessor is responsible for sending the HTTP response
                // to the client within its HandleRequest method.
                // 注意：HttpRequestProcessor 负责在其 HandleRequest 方法中
                // 向客户端发送 HTTP 响应。
            };

            // Process the request. This method contains the logic for reading body, parsing JSON, etc.
            // This executes on the current thread pool thread.
            // 处理请求。此方法包含读取请求体、解析 JSON 等逻辑。
            // 这在当前的线程池线程上执行。
            try
            {
                requestProcessor.HandleRequest();
            }
            catch (Exception e)
            {
                // Catch any unhandled exceptions during HttpRequestProcessor.HandleRequest
                // 捕获 HttpRequestProcessor.HandleRequest 期间的未处理异常
                Debug.LogError($"HttpRequestProcessor.HandleRequest 期间发生未处理异常: {e.Message}");
                // The processor should handle sending responses, but as a final fallback, ensure response is closed.
                // 处理器应自行处理响应发送，但作为最终回退，确保响应被关闭。
                try { context?.Response?.Close(); } catch { /* Ignore */ }
            }

            // The HandleHttpRequest method ends here. The thread pool thread is returned.
            // The main server loop (StartServerCoroutine), after its yield return WaitAsyncResult completes,
            // will continue waiting for the next request context.
            // HandleHttpRequest 方法在此处结束。线程池线程被返回。
            // 主服务器循环 (StartServerCoroutine) 在其 yield return WaitAsyncResult 完成后，
            // 会继续等待下一个请求上下文。
        }

        /// <summary>
        /// Notifies the server loop to stop and closes the HttpListener.
        /// Should be called from the Unity main thread (e.g., in OnDestroy).
        /// 通知服务器循环停止并关闭 HttpListener。
        /// 应从 Unity 主线程调用 (例如 OnDestroy)。
        /// </summary>
        public void StopServer()
        {
            Debug.Log("收到停止命令，正在通知服务器停止。");
            isRunning = false; // Notify the while loop in StartServerCoroutine to terminate

            // Closing the listener will cause pending BeginGetContext in the coroutine
            // and any active EndGetContext in HandleHttpRequest to throw ObjectDisposedException,
            // which we handle to exit loops/callbacks gracefully.
            // 关闭 listener 会导致协程中挂起的 BeginGetContext
            // 和 HandleHttpRequest 中任何活动的 EndGetContext 抛出 ObjectDisposedException，
            // 我们会处理这个异常来正常退出循环/回调。
            if (listener != null && listener.IsListening)
            {
                try
                {
                    // Stop receiving new connections and close existing connections
                    // 停止接收新连接并关闭现有连接
                    listener.Close();
                    Debug.Log("HttpListener 已关闭。");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"HttpListener 关闭期间发生异常: {e.Message}");
                }
            }
            listener = null; // Ensure listener reference is null
        }
    }
}