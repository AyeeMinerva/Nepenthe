using UnityEngine;
using ChatdotLive2D.HttpServer;

public class HttpServerManager : MonoBehaviour
{
    public int port = 9000;
    public static HttpServerManager Instance { get; private set; }
    public HttpServer Server { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Server = new HttpServer(port, null);
    }

    void Start()
    {
        StartCoroutine(Server.StartServerCoroutine());
    }

    void OnDestroy()
    {
        if (Server != null)
        {
            Server.StopServer();
        }
    }
}