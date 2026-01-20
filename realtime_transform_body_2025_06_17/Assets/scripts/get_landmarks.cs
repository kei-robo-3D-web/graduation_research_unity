using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.IO;

public class GetLandmarksFullBody : MonoBehaviour
{
    public WebSocketConnection connection;
    private string url = "ws://localhost:8765";
    private bool shouldReconnect = true;
    private CancellationTokenSource cts;
    public static double lastReceivedTimestamp;


    // ===== 受信結果格納 =====
    public static Vector3[] poseLandmarks = new Vector3[33];
    public static Vector3[] leftHandLandmarks = new Vector3[21];
    public static Vector3[] rightHandLandmarks = new Vector3[21];

    // ===== レイテンシログ =====
    private string latencyLogPath;

    private void Start()
    {
        cts = new CancellationTokenSource();

        // =========================
        // ログファイル初期化
        // =========================
        latencyLogPath = Path.Combine(
            Application.persistentDataPath,
            "latency_log.csv"
        );

        if (!File.Exists(latencyLogPath))
        {
            File.WriteAllText(
                latencyLogPath,
                "ReceiveTime,SendTime,LatencyMs\n"
            );
        }

        Debug.Log($"Latency log path: {latencyLogPath}");

        // =========================
        // WebSocket 初期化
        // =========================
        connection = gameObject.AddComponent<WebSocketConnection>();
        connection.DesiredConfig = new WebSocketConfig { Url = url };
        connection.Connect();

        connection.StateChanged += OnStateChanged;
        connection.MessageReceived += OnMessageReceived;
        connection.ErrorMessageReceived += OnErrorMessageReceived;

        SendPing(cts.Token).Forget();
    }

    // ==============================
    // WebSocket Events
    // ==============================
    private void OnStateChanged(
        WebSocketConnection c,
        WebSocketState oldState,
        WebSocketState newState
    )
    {
        Debug.Log($"WebSocket: {oldState} → {newState}");
        if (newState == WebSocketState.Disconnected && shouldReconnect)
            Reconnect().Forget();
    }

    private void OnMessageReceived(WebSocketConnection c, WebSocketMessage msg)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<BodyPacket>(msg.String);
            if (data == null) return;

            // =========================
            // 🔥 レイテンシ計算
            // =========================
            lastReceivedTimestamp = data.t;

            double receiveMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double latencyMs = receiveMs - data.t;

            Debug.Log($"Latency: {latencyMs:F1} ms");

            // =========================
            // 🔥 ログ保存（CSV）
            // =========================
            string line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
                $"{data.t}," +
                $"{latencyMs:F1}\n";

            File.AppendAllText(latencyLogPath, line);

            // =========================
            // Pose
            // =========================
            if (data.pose != null && data.pose.landmarks != null)
            {
                for (int i = 0; i < Mathf.Min(33, data.pose.landmarks.Length); i++)
                {
                    poseLandmarks[i] = Convert(data.pose.landmarks[i]);
                }
            }

            // =========================
            // Left Hand
            // =========================
            if (data.leftHand != null && data.leftHand.landmarks != null)
            {
                for (int i = 0; i < Mathf.Min(21, data.leftHand.landmarks.Length); i++)
                {
                    leftHandLandmarks[i] = Convert(data.leftHand.landmarks[i]);
                }
            }

            // =========================
            // Right Hand
            // =========================
            if (data.rightHand != null && data.rightHand.landmarks != null)
            {
                for (int i = 0; i < Mathf.Min(21, data.rightHand.landmarks.Length); i++)
                {
                    rightHandLandmarks[i] = Convert(data.rightHand.landmarks[i]);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON Parse Error: {e.Message}");
        }
    }

    private void OnErrorMessageReceived(WebSocketConnection c, string error)
    {
        Debug.LogError($"WebSocket Error: {error}");
    }

    // ==============================
    // Utility
    // ==============================
    private Vector3 Convert(Landmark lm)
    {
        // MediaPipe → Unity 座標変換
        return new Vector3(
            lm.x - 0.5f,
            -lm.y + 1.5f,
            lm.z
        );
    }

    private async UniTaskVoid SendPing(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (connection != null && connection.State == WebSocketState.Connected)
                connection.AddOutgoingMessage("Ping");

            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: token);
        }
    }

    private async UniTaskVoid Reconnect()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(5));
        if (connection != null && connection.State != WebSocketState.Connected)
            connection.Connect();
    }

    private void OnDestroy()
    {
        shouldReconnect = false;

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (connection != null)
        {
            connection.Disconnect();
            connection = null;
        }
    }

    // ==============================
    // JSON Classes
    // ==============================
    [Serializable]
    public class BodyPacket
    {
        public LandmarkBlock pose;
        public LandmarkBlock leftHand;
        public LandmarkBlock rightHand;
        public LandmarkBlock face;
        public double t;   // 送信時刻（ms）
    }

    [Serializable]
    public class LandmarkBlock
    {
        public Landmark[] landmarks;
    }

    [Serializable]
    public class Landmark
    {
        public float x;
        public float y;
        public float z;
    }
}
