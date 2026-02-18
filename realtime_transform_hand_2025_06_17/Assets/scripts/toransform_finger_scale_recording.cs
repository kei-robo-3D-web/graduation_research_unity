using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

public class toransform_finger_scale_recording : MonoBehaviour
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

    [Header("Recording Settings")]
    public bool isRecording = false;
    public string recordingFileName = "hand_recording";
    private List<RecordedFrame> recordedFrames = new List<RecordedFrame>();
    private float recordingStartTime = 0f;

    [Header("Playback Settings")]
    public bool isPlayback = false;
    public string playbackFileName = "hand_recording";
    private int playbackFrameIndex = 0;
    private float playbackStartTime = 0f;

    [Header("Latency Log Settings")]
    public bool saveLatencyLog = true;
    public string latencyLogFileName = "latency_log";

    private StreamWriter latencyWriter;
    private float logStartTime;

    // 記録用データ構造
    [Serializable]
    public class RecordedFrame
    {
        public float timestamp;           // 記録開始からの経過時間（秒）
        public List<List<float>> landmarks;  // 21個のランドマーク
    }

    [Serializable]
    public class RecordingData
    {
        public List<RecordedFrame> frames;
        public float duration;
        public int frameCount;
        public string recordedDate;
    }

    private void Start()
    {
        Debug.Log("=== HandTracking Start ===");

        if (saveLatencyLog)
        {
            string path = Path.Combine(Application.dataPath, latencyLogFileName + ".csv");
            latencyWriter = new StreamWriter(path, false);
            latencyWriter.WriteLine("frame,time_sec,latency_ms,avg_latency_ms");
            latencyWriter.Flush();

            logStartTime = Time.time;

            Debug.Log($"📄 Latency log started: {path}");
        }


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

    private void Update()
    {
        // 🔥 R キーで記録開始/停止
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }

        // 🔥 P キーで再生開始/停止
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (!isPlayback)
            {
                StartPlayback();
            }
            else
            {
                StopPlayback();
            }
        }

        // 🔥 再生処理
        if (isPlayback && recordedFrames.Count > 0)
        {
            float currentTime = Time.time - playbackStartTime;
            PlaybackFrame(currentTime);
        }
    }

    private void StartRecording()
    {
        isRecording = true;
        recordedFrames.Clear();
        recordingStartTime = Time.time;

        // 🔥 遅延測定をここから開始
        totalLatency = 0f;
        latencyCount = 0;
        currentLatency = 0f;
        minLatency = float.MaxValue;
        maxLatency = 0f;

        logStartTime = Time.time;

        Debug.Log("🔴 記録開始（遅延測定リセット）");
    }

    private void StopRecording()
    {
        isRecording = false;
        SaveRecording();
        Debug.Log($"⏹️ 記録停止 - {recordedFrames.Count} フレーム保存");
    }

    private void SaveRecording()
    {
        if (recordedFrames.Count == 0)
        {
            Debug.LogWarning("⚠ 記録データがありません");
            return;
        }

        RecordingData data = new RecordingData
        {
            frames = recordedFrames,
            duration = recordedFrames[recordedFrames.Count - 1].timestamp,
            frameCount = recordedFrames.Count,
            recordedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string filePath = Path.Combine(Application.dataPath, $"{recordingFileName}.json");
        File.WriteAllText(filePath, json);

        Debug.Log($"💾 保存完了: {filePath}");
        Debug.Log($"📊 記録時間: {data.duration:F2}秒 / フレーム数: {data.frameCount}");
    }

    private void StartPlayback()
    {
        string filePath = Path.Combine(Application.dataPath, $"{playbackFileName}.json");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"❌ ファイルが見つかりません: {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        RecordingData data = JsonConvert.DeserializeObject<RecordingData>(json);

        recordedFrames = data.frames;
        playbackFrameIndex = 0;
        playbackStartTime = Time.time;
        isPlayback = true;

        Debug.Log($"▶️ 再生開始: {data.frameCount} フレーム / {data.duration:F2}秒");
    }

    private void StopPlayback()
    {
        isPlayback = false;
        Debug.Log("⏹️ 再生停止");
    }

    private void PlaybackFrame(float currentTime)
    {
        // 現在時刻に最も近いフレームを探す
        for (int i = playbackFrameIndex; i < recordedFrames.Count; i++)
        {
            if (recordedFrames[i].timestamp >= currentTime)
            {
                playbackFrameIndex = i;
                ApplyLandmarks(recordedFrames[i].landmarks);
                return;
            }
        }

        // 最後まで再生したらループ
        playbackFrameIndex = 0;
        playbackStartTime = Time.time;
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

            // 遅延計算
            if (measureLatency && isRecording && data.t > 0)
            {
                currentLatency = (float)(receiveTime - data.t);
                totalLatency += currentLatency;
                latencyCount++;

                if (currentLatency < minLatency) minLatency = currentLatency;
                if (currentLatency > maxLatency) maxLatency = currentLatency;

                float avgLatency = totalLatency / latencyCount;

                // 🔥 ここを追加
                if (saveLatencyLog && latencyWriter != null)
                {
                    float elapsed = Time.time - logStartTime;
                    latencyWriter.WriteLine(
                        $"{latencyCount},{elapsed:F3},{currentLatency:F2},{avgLatency:F2}"
                    );
                }

                if (messageCount % 30 == 1)
                {
                    
                    Debug.Log($"⏱️ 遅延: 現在={currentLatency:F1}ms / 平均={avgLatency:F1}ms");
                }
            }

            // ランドマークを取得
            List<List<float>> landmarksList = null;

            if (data.lh != null && data.lh.Count >= 21)
            {
                landmarksList = data.lh;
            }
            else if (data.hands != null && data.hands.Count >= 21)
            {
                // 旧形式を新形式に変換
                landmarksList = new List<List<float>>();
                foreach (var lm in data.hands)
                {
                    landmarksList.Add(new List<float> { lm.x, lm.y, lm.z });
                }
            }
            else
            {
                Debug.LogWarning($"⚠ ランドマーク不足");
                return;
            }

            // 🔥 記録中なら保存
            if (isRecording)
            {
                RecordedFrame frame = new RecordedFrame
                {
                    timestamp = Time.time - recordingStartTime,
                    landmarks = landmarksList
                };
                recordedFrames.Add(frame);
            }

            // 🔥 再生中でなければライブデータを適用
            if (!isPlayback)
            {
                ApplyLandmarks(landmarksList);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ エラー: {ex.Message}");
        }
    }

    private void ApplyLandmarks(List<List<float>> landmarksList)
    {
        Vector3[] landmarks = new Vector3[21];

        for (int i = 0; i < 21; i++)
        {
            landmarks[i] = new Vector3(
                landmarksList[i][0],
                -landmarksList[i][1],
                landmarksList[i][2]
            );
        }

        // スムージング適用
        if (!isFirstFrame)
        {
            for (int i = 0; i < 21; i++)
            {
                landmarks[i] = Vector3.Lerp(landmarks[i], previousLandmarks[i], smoothingFactor);
            }
        }
        else
        {
            isFirstFrame = false;
        }

        for (int i = 0; i < 21; i++)
        {
            previousLandmarks[i] = landmarks[i];
        }

        // 手首の位置とモデルへの適用（既存のコード）
        euclidDistance = Vector3.Distance(landmarks[5], landmarks[0]);

        if (handTransform[0] != null)
        {
            handTransform[0].position = new Vector3(
                landmarks[0].x,
                landmarks[0].y,
                landmarks[0].z + (euclidDistance * 20.0f)
            );
        }

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

        finger_rotation(handTransform, landmarks, 1, 4);
        finger_rotation(handTransform, landmarks, 5, 8);
        finger_rotation(handTransform, landmarks, 9, 12);
        finger_rotation(handTransform, landmarks, 13, 16);
        finger_rotation(handTransform, landmarks, 17, 20);
    }

    private void OnGUI()
    {
        // 既存の遅延表示
        if (measureLatency && latencyCount > 0)
        {
            float avgLatency = totalLatency / latencyCount;

            GUI.Box(new Rect(5, 5, 350, 110), "");

            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.green;

            if (currentLatency > 100)
            {
                style.normal.textColor = Color.red;
            }
            else if (currentLatency > 50)
            {
                style.normal.textColor = Color.yellow;
            }

            GUI.Label(new Rect(15, 15, 500, 30), $"Current: {currentLatency:F1} ms", style);

            style.normal.textColor = Color.white;
            GUI.Label(new Rect(15, 40, 500, 30), $"Average: {avgLatency:F1} ms", style);

            style.fontSize = 16;
            GUI.Label(new Rect(15, 65, 500, 25), $"Min: {minLatency:F1} / Max: {maxLatency:F1} ms", style);

            style.fontSize = 14;
            style.normal.textColor = Color.cyan;
            GUI.Label(new Rect(15, 90, 500, 20), $"Smoothing: {smoothingFactor:F2}", style);
        }

        // 🔥 記録/再生状態の表示
        GUIStyle recordStyle = new GUIStyle();
        recordStyle.fontSize = 18;
        recordStyle.fontStyle = FontStyle.Bold;

        if (isRecording)
        {
            recordStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(10, 120, 300, 30), $"🔴 REC {recordedFrames.Count} frames", recordStyle);
        }

        if (isPlayback)
        {
            recordStyle.normal.textColor = Color.green;
            float progress = playbackFrameIndex / (float)recordedFrames.Count * 100f;
            GUI.Label(new Rect(10, 150, 300, 30), $"▶️ PLAY {progress:F0}%", recordStyle);
        }

        // 操作説明
        GUIStyle helpStyle = new GUIStyle();
        helpStyle.fontSize = 14;
        helpStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(10, Screen.height - 60, 400, 25), "R: Record/Stop", helpStyle);
        GUI.Label(new Rect(10, Screen.height - 35, 400, 25), "P: Playback/Stop", helpStyle);
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
        if (latencyWriter != null)
        {
            latencyWriter.Flush();
            latencyWriter.Close();
            latencyWriter = null;
            Debug.Log("📄 Latency log saved");
        }
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
        public List<List<float>> lh;             // 新形式（圧縮版）
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