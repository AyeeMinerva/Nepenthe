using System;
using System.Collections;
using System.Net;
using UnityEngine; // ��Ҫ Debug, Coroutine ֧��, �� WaitForSeconds
using ChatdotLive2D.Data; // ��Ҫ EmotionMapping (����ֱ��ʹ�����ĵط�����)
using ChatdotLive2D.Utilities; // ��Ҫ WaitAsyncResult
// ��� HttpRequestProcessor ��Ҫ��Щģ�ͣ�������Ҫ��������ģ�������ռ�
using ChatdotLive2D.HttpServer.DataModels; // ��Ҫ��������ģ�������ռ���ʹ�� TextChunkCommand

namespace ChatdotLive2D.HttpServer
{
    /// <summary>
    /// Manages HttpListener lifecycle and dispatches requests to HttpRequestProcessor.
    /// Receives text chunks from processors and signals to the main thread.
    /// ���� HttpListener �������ڣ�����������ɸ� HttpRequestProcessor ����
    /// �Ӵ����������ı��鲢�ź�֪ͨ���̡߳�
    /// </summary>
    public class HttpServer
    {
        private HttpListener listener;
        private bool isRunning = false;
        private readonly int port;
        // EmotionMapping is passed to HttpRequestProcessor, but HttpServer itself
        // no longer uses it directly in the event handler.
        // EmotionMapping �����ݸ� HttpRequestProcessor���� HttpServer �Լ�
        // ���¼��������в���ֱ��ʹ������
        private readonly EmotionMapping emotionMapping;

        /// <summary>
        /// Event triggered when a text chunk command is successfully processed.
        /// Signals the main thread with the received text chunk.
        /// ���ı�������ɹ�����󴥷���
        /// �ý��յ����ı���֪ͨ���̡߳�
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
        /// ���� HttpListener �������������ѭ����Э�̡�
        /// ��Ҫͨ�� MonoBehaviour �� StartCoroutine ������
        /// </summary>
        public IEnumerator StartServerCoroutine()
        {
            bool startupFailed = false;

            // --- ���������� ---
            Debug.Log($"�����ڶ˿� {port} ���� HTTP ������...");
            try
            {
                listener = new HttpListener();
                // Use '+' to listen on all available IPs, not just localhost
                // ʹ�� '+' ����������п��� IP������������ localhost
                listener.Prefixes.Add($"http://+:{port}/");
                listener.Start();
                isRunning = true;
                Debug.Log($"HTTP �������ڶ˿� {port} �����ɹ�����ʼ��������");
            }
            catch (Exception e)
            {
                Debug.LogError($"���� HTTP Listener ʧ�ܣ��˿� {port}: {e.Message}");
                isRunning = false;
                startupFailed = true;
            }

            if (startupFailed)
            {
                Debug.LogError("HTTP ����������ʧ�ܣ��˳�Э�̡�");
                yield break;
            }

            // --- ����������ѭ�� ---
            Debug.Log("HTTP ����������ѭ����ʼ��");
            while (isRunning)
            {
                IAsyncResult result = null;
                bool errorDuringGetContext = false;

                try
                {
                    // Asynchronously begin waiting for an incoming request.
                    // When a request arrives, the HandleHttpRequest callback is invoked on a thread pool thread.
                    // �첽��ʼ�ȴ���������
                    // ���󵽴��HandleHttpRequest �ص������̳߳��߳��ϱ����á�
                    result = listener.BeginGetContext(HandleHttpRequest, listener);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was closed while BeginGetContext was pending, normal loop exit
                    // Listener �� BeginGetContext ����ʱ���رգ������˳�ѭ��
                    Debug.Log("Listener �� BeginGetContext �ڼ䱻�ͷţ����������˳�������ѭ����");
                    isRunning = false;
                    break;
                }
                catch (Exception e) // Catch other exceptions from BeginGetContext
                {
                    Debug.LogError($"listener.BeginGetContext �ڼ䷢������: {e.Message}");
                    errorDuringGetContext = true;
                }

                if (errorDuringGetContext)
                {
                    // If an error occurred, wait a short time before trying to get the next context
                    // ����������󣬵ȴ�һС��ʱ���ٳ��Ի�ȡ��һ��������
                    Debug.Log("������һ�����󣬵ȴ� 1 ����ٴγ��� BeginGetContext��");
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // If BeginGetContext succeeded and returned a result, yield until the operation completes.
                // This yields control of the coroutine back to the Unity main thread until the request context is fully available.
                // ��� BeginGetContext �ɹ������ؽ�����ȴ�������ɡ�
                // ��ὫЭ�̵�ִ��Ȩ������ Unity ���̣߳�ֱ��������������ȫ���á�
                if (result != null)
                {
                    // The coroutine will pause here until the async operation (getting context) finishes
                    // Э�̻��ڴ˴���ͣ��ֱ���첽���� (��ȡ������) ���
                    yield return new ChatdotLive2D.Utilities.WaitAsyncResult(result);
                    // Note: The HandleHttpRequest callback will be invoked when the system is ready with the context,
                    // potentially before the WaitAsyncResult completes, but this yield ensures
                    // the loop doesn't immediately start waiting for the next context.
                    // ע�⣺HandleHttpRequest �ص�����ϵͳ׼����������ʱ���ã�
                    // ������ WaitAsyncResult ���֮ǰ�����˴��� yield ȷ��
                    // ѭ������������ʼ�ȴ���һ�������ġ�
                }
                else
                {
                    // As a safeguard, if no exception was thrown but result is null (unlikely), wait a frame.
                    // ���û���׳��쳣�� result Ϊ null (��̫����)����Ϊ safeguard �ȴ�һ֡��
                    Debug.LogWarning("listener.BeginGetContext ���� null ��δ������ȷ�쳣���ȴ�һ֡��");
                    yield return null;
                }
            }

            // --- Cleanup after loop exit ---
            // --- ѭ���˳�������� ---
            Debug.Log("HTTP ������ѭ���������������� listener��");
            // If the loop exited due to ObjectDisposedException, the listener should already be closed.
            // This ensures cleanup in other exit scenarios or if closing failed in catch.
            // ���ѭ������ ObjectDisposedException �˳���listener Ӧ���ѹرա�
            // �˴�ȷ���������˳������� catch �йر�ʧ��ʱ��������
            if (listener != null && listener.IsListening)
            {
                try
                {
                    // Stop receiving new requests and close existing connections/streams
                    // ֹͣ���������Ӳ��ر���������/��
                    listener.Stop(); // Stop receiving new connections
                    Debug.Log("HTTP ������ listener ��ֹͣ��");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"listener.Stop �ڼ䷢���쳣: {e.Message}");
                }

                try
                {
                    listener.Close(); // Release resources
                    Debug.Log("HTTP ������ listener �ѹرա�");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"listener.Close �ڼ䷢���쳣: {e.Message}");
                }
            }
            listener = null; // Dereference listener

            Debug.Log("HTTP ������Э���ѽ�����");
        }

        /// <summary>
        /// Called by HttpListener on a thread pool thread when a request context is available.
        /// �� HttpListener ���̳߳��߳��ϵ��ã������������Ŀ���ʱִ�С�
        /// </summary>
        /// <param name="result">The async operation result containing the context.</param>
        private void HandleHttpRequest(IAsyncResult result)
        {
            // Ensure listener is still active and listening before processing
            // �ڴ���ǰȷ�� listener ��Ȼ��Ծ������
            HttpListener listener = (HttpListener)result.AsyncState;
            if (!isRunning || listener == null || !listener.IsListening)
            {
                Debug.LogWarning("HandleHttpRequest �����ã���������δ���С�listener Ϊ null ��δ������");
                // Attempt to get context and close response to avoid resource leaks, but handle EndGetContext potentially throwing.
                // ���Ի�ȡ�����Ĳ��ر���Ӧ��������Դй©������С�� EndGetContext �������쳣��
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
                // ����첽��������ȡ������
                context = listener.EndGetContext(result);
            }
            catch (ObjectDisposedException)
            {
                // listener was closed after BeginGetContext but before EndGetContext, handled by loop exit logic.
                // listener �� BeginGetContext ���� EndGetContext ǰ�رգ�����ѭ���˳��߼�����
                Debug.Log("listener �ڻص��� EndGetContext �ڼ䱻�ͷš�");
                return;
            }
            catch (Exception e)
            {
                // Handle other potential errors getting context
                // �����ȡ�����ĵ�����Ǳ�ڴ���
                Debug.LogError($"�ڻص��л�ȡ HTTP �����Ĵ���: {e.Message}");
                // Cannot process request without context, just return.
                // û���������޷���������ֱ�ӷ��ء�
                return;
            }

            // Create a processor for this specific request context
            // Ϊ���ض����������Ĵ���һ��������
            var requestProcessor = new HttpRequestProcessor(context, emotionMapping);

            // Subscribe to the processor's event to get the processing result
            // This callback occurs on the thread pool thread where HandleHttpRequest resides.
            // ���Ĵ��������¼�����ȡ������
            // �˻ص������� HandleHttpRequest ���ڵ��̳߳��߳��ϡ�
            // *** MODIFIED: Subscribing to the new event name and updated lambda parameters ***
            requestProcessor.OnCommandProcessed += (success, textChunk, errorMessage) =>
            {
                if (success)
                {
                    // If the request was processed successfully (JSON parsed, chunk extracted),
                    // trigger our own event to notify the main thread controller to queue the command.
                    // �������ɹ����� (JSON �������ı�����ȡ)��
                    // ���������Լ����¼���֪ͨ���߳̿������Ŷ�ִ�����
                    Debug.Log($"HttpServer: Command processed successfully. Notifying main thread with chunk: '{textChunk}'"); // Added log
                    // *** MODIFIED: Invoke the new event, passing the text chunk ***
                    OnTextChunkReceived?.Invoke(textChunk);
                    // Note: The logic to map the textChunk to an animation input (int)
                    // should now happen in the main thread subscriber of OnTextChunkReceived.
                    // ע�⣺�� textChunk ӳ�䵽��������ֵ (int) ���߼�
                    // ����Ӧ���� OnTextChunkReceived �¼������̶߳�������ʵ�֡�
                }
                else
                {
                    // Optional: Log or handle errors from the processor if needed in HttpServer
                    // ��ѡ����� HttpServer ��Ҫ���������еĴ��󣬿��������������־�����߼�
                    Debug.LogWarning($"HttpServer: RequestProcessor reported failure. Message: {errorMessage}"); // Added log
                }
                // Note: HttpRequestProcessor is responsible for sending the HTTP response
                // to the client within its HandleRequest method.
                // ע�⣺HttpRequestProcessor �������� HandleRequest ������
                // ��ͻ��˷��� HTTP ��Ӧ��
            };

            // Process the request. This method contains the logic for reading body, parsing JSON, etc.
            // This executes on the current thread pool thread.
            // �������󡣴˷���������ȡ�����塢���� JSON ���߼���
            // ���ڵ�ǰ���̳߳��߳���ִ�С�
            try
            {
                requestProcessor.HandleRequest();
            }
            catch (Exception e)
            {
                // Catch any unhandled exceptions during HttpRequestProcessor.HandleRequest
                // ���� HttpRequestProcessor.HandleRequest �ڼ��δ�����쳣
                Debug.LogError($"HttpRequestProcessor.HandleRequest �ڼ䷢��δ�����쳣: {e.Message}");
                // The processor should handle sending responses, but as a final fallback, ensure response is closed.
                // ������Ӧ���д�����Ӧ���ͣ�����Ϊ���ջ��ˣ�ȷ����Ӧ���رա�
                try { context?.Response?.Close(); } catch { /* Ignore */ }
            }

            // The HandleHttpRequest method ends here. The thread pool thread is returned.
            // The main server loop (StartServerCoroutine), after its yield return WaitAsyncResult completes,
            // will continue waiting for the next request context.
            // HandleHttpRequest �����ڴ˴��������̳߳��̱߳����ء�
            // ��������ѭ�� (StartServerCoroutine) ���� yield return WaitAsyncResult ��ɺ�
            // ������ȴ���һ�����������ġ�
        }

        /// <summary>
        /// Notifies the server loop to stop and closes the HttpListener.
        /// Should be called from the Unity main thread (e.g., in OnDestroy).
        /// ֪ͨ������ѭ��ֹͣ���ر� HttpListener��
        /// Ӧ�� Unity ���̵߳��� (���� OnDestroy)��
        /// </summary>
        public void StopServer()
        {
            Debug.Log("�յ�ֹͣ�������֪ͨ������ֹͣ��");
            isRunning = false; // Notify the while loop in StartServerCoroutine to terminate

            // Closing the listener will cause pending BeginGetContext in the coroutine
            // and any active EndGetContext in HandleHttpRequest to throw ObjectDisposedException,
            // which we handle to exit loops/callbacks gracefully.
            // �ر� listener �ᵼ��Э���й���� BeginGetContext
            // �� HandleHttpRequest ���κλ�� EndGetContext �׳� ObjectDisposedException��
            // ���ǻᴦ������쳣�������˳�ѭ��/�ص���
            if (listener != null && listener.IsListening)
            {
                try
                {
                    // Stop receiving new connections and close existing connections
                    // ֹͣ���������Ӳ��ر���������
                    listener.Close();
                    Debug.Log("HttpListener �ѹرա�");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"HttpListener �ر��ڼ䷢���쳣: {e.Message}");
                }
            }
            listener = null; // Ensure listener reference is null
        }
    }
}