using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;

public class toransform_finger_scale_smooth_Ver1 : MonoBehaviour
{
    public WebSocketConnection _connection;
    private string _url = "ws://localhost:8765";
    private bool _shouldReconnect = true;
    private CancellationTokenSource _cts;

    public GameObject[] hand = new GameObject[21];
    Transform[] handTransform = new Transform[21];

    HandData receivedJson;

    Vector3 euclid;
    float euclidDistance = 0.0f;

    Vector3[] nowTransform = new Vector3[21];

    // デバッグ用フラグ
    [Header("Debug Settings")]
    public bool showDebugLogs = true;
    private int messageCount = 0;

    [Header("Smoothing Settings")]
    [Range(0f, 1f)]
    public float smoothingFactor = 0.5f;  // 0=スムージングなし, 1=最大スムージング

    private Vector3[] previousLandmarks = new Vector3[21];
    private bool isFirstFrame = true;

    [Header("Latency Measurement")]
    public bool measureLatency = true;
    private float totalLatency = 0f;
    private int latencyCount = 0;
    private float currentLatency = 0f;
    private float minLatency = float.MaxValue;
    private float maxLatency = 0f;


    private void Start()
    {
        Debug.Log("=== HandTracking Start ===");

        _cts = new CancellationTokenSource();

        _connection = gameObject.AddComponent<WebSocketConnection>();
        _connection.DesiredConfig = new WebSocketConfig { Url = _url };
        _connection.Connect();
        _connection.StateChanged += OnStateChanged;
        _connection.MessageReceived += OnMessageReceived;
        _connection.ErrorMessageReceived += OnErrorMessageReceived;

        Debug.Log($"WebSocket接続開始: {_url}");

        SendMessagesPeriodically(_cts.Token).Forget();

        // 手のGameObjectを取得
        int foundCount = 0;
        for (int i = 0; i < 21; i++)
        {
            if (i == 4 || i == 8 || i == 12 || i == 16 || i == 20)
            {
                if (i < 10 && i != 0)
                {
                    hand[i] = GameObject.Find("hand.00" + (i - 1).ToString() + "_end");
                }
                else
                {
                    hand[i] = GameObject.Find("hand.0" + (i - 1).ToString() + "_end");
                }
            }
            else
            {
                if (i < 10)
                {
                    hand[i] = GameObject.Find("hand.00" + (i).ToString());
                }
                else
                {
                    hand[i] = GameObject.Find("hand.0" + (i).ToString());
                }
            }

            if (hand[i] != null)
            {
                handTransform[i] = hand[i].transform;
                nowTransform[i] = handTransform[i].eulerAngles;
                foundCount++;
            }
            else
            {
                Debug.LogWarning($"⚠ hand[{i}] GameObject not found!");
            }
        }

        Debug.Log($"✅ {foundCount}/21 hand GameObjects found");
    }

    private void OnStateChanged(WebSocketConnection connection, WebSocketState oldState, WebSocketState newState)
    {
        Debug.Log($"🔄 WebSocket state: {oldState} → {newState}");

        if (newState == WebSocketState.Connected)
        {
            Debug.Log("✅ WebSocket接続成功！");
        }
        else if (newState == WebSocketState.Disconnected && _shouldReconnect)
        {
            Debug.LogWarning("🔴 WebSocket切断 - 再接続試行...");
            Reconnect().Forget();
        }
    }

    private void OnMessageReceived(WebSocketConnection connection, WebSocketMessage message)
    {
        // 🔥 受信時刻を記録（ミリ秒）
        double receiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        messageCount++;

        if (showDebugLogs && messageCount % 30 == 1)
        {
            Debug.Log($"📨 メッセージ受信 #{messageCount}");
        }

        string messageString = message.String;

        if (string.IsNullOrEmpty(messageString) || !messageString.StartsWith("{"))
        {
            return;
        }

        try
        {
            var data = JsonConvert.DeserializeObject<HandData>(messageString);

            if (data == null)
            {
                Debug.LogError("❌ デシリアライズ結果がnull");
                return;
            }

            // 🔥 遅延計算
            if (measureLatency && data.t > 0)
            {
                currentLatency = (float)(receiveTime - data.t);
                totalLatency += currentLatency;
                latencyCount++;

                if (currentLatency < minLatency) minLatency = currentLatency;
                if (currentLatency > maxLatency) maxLatency = currentLatency;

                // 30回に1回詳細表示
                if (messageCount % 30 == 1)
                {
                    float avgLatency = totalLatency / latencyCount;
                    Debug.Log($"⏱️ 遅延: 現在={currentLatency:F1}ms / 平均={avgLatency:F1}ms / 最小={minLatency:F1}ms / 最大={maxLatency:F1}ms");
                }
            }

            // ランドマークを取得
            Vector3[] landmarks = new Vector3[21];

            if (data.h != null && data.h.Count >= 21)
            {
                for (int i = 0; i < 21; i++)
                {
                    landmarks[i] = new Vector3(
                        data.h[i][0],
                        -data.h[i][1],
                        data.h[i][2]
                    );
                }
            }
            else if (data.hands != null && data.hands.Count >= 21)
            {
                for (int i = 0; i < 21; i++)
                {
                    landmarks[i] = new Vector3(
                        data.hands[i].x,
                        -data.hands[i].y,
                        data.hands[i].z
                    );
                }
            }
            else
            {
                Debug.LogWarning($"⚠ ランドマーク不足");
                return;
            }

            //// 🔥 スムージング適用
            //if (!isFirstFrame)
            //{
            //    for (int i = 0; i < 21; i++)
            //    {
            //        landmarks[i] = Vector3.Lerp(landmarks[i], previousLandmarks[i], smoothingFactor);
            //    }
            //}
            //else
            //{
            //    isFirstFrame = false;
            //}

            // 🔥 現在のランドマークを保存
            for (int i = 0; i < 21; i++)
            {
                previousLandmarks[i] = landmarks[i];
            }

            if (messageCount == 1)
            {
                Debug.Log($"✅ 初回データ受信成功！");
                Debug.Log($"   手首座標: ({landmarks[0].x:F3}, {landmarks[0].y:F3}, {landmarks[0].z:F3})");
            }

            // スケール計算
            euclidDistance = Vector3.Distance(landmarks[5], landmarks[0]);

            // 手首の位置設定
            if (handTransform[0] != null)
            {
                handTransform[0].position = new Vector3(
                    landmarks[0].x,
                    landmarks[0].y,
                    landmarks[0].z + (euclidDistance * 20.0f)
                );
            }

            // 手のひら回転
            Vector3 wrist = landmarks[0];
            Vector3 indexBase = landmarks[5];
            Vector3 pinkyBase = landmarks[17];
            Vector3 dir1 = (indexBase - wrist).normalized;
            Vector3 dir2 = (pinkyBase - wrist).normalized;
            Vector3 palmNormal = Vector3.Cross(dir1, dir2).normalized;

            if (handTransform[0] != null)
            {
                handTransform[0].rotation = Quaternion.LookRotation(palmNormal, dir1);
            }

            // 各指の回転
            finger_rotation(handTransform, landmarks, 1, 4);
            finger_rotation(handTransform, landmarks, 5, 8);
            finger_rotation(handTransform, landmarks, 9, 12);
            finger_rotation(handTransform, landmarks, 13, 16);
            finger_rotation(handTransform, landmarks, 17, 20);
        }
        catch (JsonException ex)
        {
            Debug.LogError($"❌ JSON解析エラー: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 予期しないエラー: {ex.Message}");
        }
    }

    void finger_rotation(Transform[] handTransform, Vector3[] landmarks, int baseFinger, int end)
    {
        try
        {
            float straightDistance = Vector3.Distance(landmarks[baseFinger], landmarks[end]);
            float totalJointDistance = Vector3.Distance(landmarks[baseFinger], landmarks[baseFinger + 1])
                                     + Vector3.Distance(landmarks[baseFinger + 1], landmarks[baseFinger + 2])
                                     + Vector3.Distance(landmarks[baseFinger + 2], landmarks[end]);

            float bendRatio = straightDistance / totalJointDistance;
            float angle = (1f - bendRatio) * 120.0f;

            // デバッグ：最初の指（親指）のみログ出力
            if (showDebugLogs && baseFinger == 1 && messageCount <= 3)
            {
                Debug.Log($"指回転 [{baseFinger}-{end}]: 角度={angle:F1}°, 曲げ率={bendRatio:F2}");
            }

            for (int i = baseFinger; i <= end; i++)
            {
                if (handTransform[i] == null)
                {
                    if (showDebugLogs && messageCount == 1)
                    {
                        Debug.LogWarning($"⚠ handTransform[{i}] is null");
                    }
                    continue;
                }

                if (i == 1)
                {
                    handTransform[i].localRotation = Quaternion.Euler(angle, -69f, -26.5f);
                }
                else
                {
                    handTransform[i].localRotation = Quaternion.Euler(angle, 0, 0);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ finger_rotation エラー [{baseFinger}-{end}]: {ex.Message}");
        }
    }

    // 🔥 GUI表示（画面上に遅延を表示）
    private void OnGUI()
    {
        if (!measureLatency || latencyCount == 0) return;

        float avgLatency = totalLatency / latencyCount;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.green;

        // 遅延の色分け
        if (currentLatency > 100)
        {
            style.normal.textColor = Color.red;
        }
        else if (currentLatency > 50)
        {
            style.normal.textColor = Color.yellow;
        }

        GUI.Label(new Rect(10, 10, 500, 30), $"Current Latency: {currentLatency:F1} ms", style);

        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 35, 500, 30), $"Average Latency: {avgLatency:F1} ms", style);
        GUI.Label(new Rect(10, 60, 500, 30), $"Min: {minLatency:F1} ms / Max: {maxLatency:F1} ms", style);

        // スムージング設定も表示
        style.fontSize = 16;
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(10, 90, 500, 25), $"Smoothing: {smoothingFactor:F2}", style);
    }

    private void OnErrorMessageReceived(WebSocketConnection connection, string errorMessage)
    {
        Debug.LogError($"❌ WebSocket Error: {errorMessage}");
    }

    private async UniTaskVoid SendMessagesPeriodically(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_connection != null && _connection.State == WebSocketState.Connected)
            {
                var message = "Ping from Unity";
                _connection.AddOutgoingMessage(message);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
        }
    }

    private async UniTaskVoid Reconnect()
    {
        Debug.Log("🔄 再接続試行中...");
        await UniTask.Delay(TimeSpan.FromSeconds(5));
        if (_connection != null && _connection.State != WebSocketState.Connected)
        {
            _connection.Connect();
        }
    }

    private void OnDestroy()
    {
        Debug.Log("=== HandTracking Destroy ===");
        _cts.Cancel();
        _cts.Dispose();
        _shouldReconnect = false;
        if (_connection != null)
        {
            _connection.Disconnect();
            _connection = null;
        }
    }

    [Serializable]
    public class HandData
    {
        public List<ReceivedJson> hands;        // 旧形式
        public List<List<float>> h;             // 新形式（圧縮版）
        public double t;                        // 🔥 タイムスタンプ（送信時刻 in milliseconds）
    }

    [Serializable]
    public class ReceivedJson
    {
        public float x;
        public float y;
        public float z;
    }
}