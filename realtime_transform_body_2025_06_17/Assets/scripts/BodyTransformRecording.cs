using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class BodyTransformRecording : MonoBehaviour
{
    [Header("Target")]
    public Animator animator;

    private HumanPoseHandler poseHandler;
    private HumanPose humanPose;

    // =======================
    // Recording / Playback
    // =======================

    [Header("Recording")]
    public bool isRecording = false;
    public bool isPlayback = false;
    public string fileName = "body_muscle_recording";

    private float recordStartTime;
    private float playbackStartTime;
    private int playbackIndex;

    private List<RecordedFrame> frames = new List<RecordedFrame>();

    // =======================
    // 🔥 Latency Measurement
    // =======================

    [Header("Latency Recording")]
    public bool isLatencyRecording = false;
    private List<string> latencyBuffer = new List<string>();
    private string latencyLogPath;

    private int latencyCount = 0;
    private float totalLatencySum = 0f;
    private float minLatency = float.MaxValue;
    private float maxLatency = 0f;
    private float currentLatency = 0f;

    // =======================
    // 記録用データ構造
    // =======================

    [Serializable]
    public class RecordedFrame
    {
        public float timestamp;
        public List<float> muscles;
    }

    [Serializable]
    public class RecordingData
    {
        public List<RecordedFrame> frames;
        public int frameCount;
        public float duration;
        public string recordedDate;

        // 🔥 遅延統計も保存
        public float avgLatency;
        public float minLatency;
        public float maxLatency;
        public int latencySamples;
    }

    // =======================
    // Unity
    // =======================

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        humanPose = new HumanPose();

        // 🔥 CSV 保存先
        latencyLogPath = Path.Combine(
            Application.persistentDataPath,
            "body_total_latency_log.csv"
        );

        if (!File.Exists(latencyLogPath))
        {
            File.WriteAllText(latencyLogPath,
                "Time,SendTime,TotalLatencyMs\n");
        }

        Debug.Log($"Latency log path: {latencyLogPath}");
    }

    void Update()
    {
        // =======================
        // R : Record + Latency Start / Stop
        // =======================
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRecording)
                StartRecording();
            else
                StopRecording();
        }

        // =======================
        // P : Playback
        // =======================
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (!isPlayback)
                StartPlayback();
            else
                StopPlayback();
        }

        if (isRecording)
        {
            RecordFrame();
            MeasureLatency();   // 🔥 同時に遅延測定
        }

        if (isPlayback)
        {
            PlaybackFrame();
        }
    }

    // =======================
    // Recording
    // =======================

    void StartRecording()
    {
        // --- モーション記録 ---
        frames.Clear();
        recordStartTime = Time.time;
        isRecording = true;

        // --- 🔥 遅延測定初期化 ---
        StartLatencyRecording();

        Debug.Log("🔴 Body Recording + Latency Start");
    }

    void StopRecording()
    {
        isRecording = false;

        StopLatencyRecording();
        SaveToJson();

        Debug.Log($"⏹ Recording Stop ({frames.Count} frames)");
    }

    void RecordFrame()
    {
        poseHandler.GetHumanPose(ref humanPose);

        RecordedFrame frame = new RecordedFrame
        {
            timestamp = Time.time - recordStartTime,
            muscles = new List<float>(humanPose.muscles)
        };

        frames.Add(frame);
    }

    // =======================
    // 🔥 Latency Measurement
    // =======================

    void StartLatencyRecording()
    {
        isLatencyRecording = true;

        latencyBuffer.Clear();
        latencyCount = 0;
        totalLatencySum = 0f;
        minLatency = float.MaxValue;
        maxLatency = 0f;

        Debug.Log("⏱ Latency Recording START");
    }

    void StopLatencyRecording()
    {
        isLatencyRecording = false;

        // CSV に一括保存
        if (latencyBuffer.Count > 0)
        {
            File.AppendAllLines(latencyLogPath, latencyBuffer);
            latencyBuffer.Clear();
        }

        float avg = (latencyCount > 0) ? totalLatencySum / latencyCount : 0f;

        Debug.Log("⏱ Latency Recording STOP");
        Debug.Log($"📊 Samples: {latencyCount}");
        Debug.Log($"📊 Avg: {avg:F1} ms");
        Debug.Log($"📊 Min: {minLatency:F1} ms");
        Debug.Log($"📊 Max: {maxLatency:F1} ms");
    }

    void MeasureLatency()
    {
        if (!isLatencyRecording) return;

        double nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        double totalLatency = nowMs - GetLandmarksFullBody.lastReceivedTimestamp;

        currentLatency = (float)totalLatency;

        latencyCount++;
        totalLatencySum += currentLatency;

        if (currentLatency < minLatency) minLatency = currentLatency;
        if (currentLatency > maxLatency) maxLatency = currentLatency;

        // CSV バッファ
        string line =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
            $"{GetLandmarksFullBody.lastReceivedTimestamp}," +
            $"{currentLatency:F1}";

        latencyBuffer.Add(line);
    }

    // =======================
    // Playback
    // =======================

    void StartPlayback()
    {
        LoadFromJson();
        playbackIndex = 0;
        playbackStartTime = Time.time;
        isPlayback = true;

        Debug.Log("▶ Body Playback Start");
    }

    void StopPlayback()
    {
        isPlayback = false;
        Debug.Log("⏹ Body Playback Stop");
    }

    void PlaybackFrame()
    {
        if (frames.Count == 0) return;

        float currentTime = Time.time - playbackStartTime;

        while (playbackIndex < frames.Count &&
               frames[playbackIndex].timestamp <= currentTime)
        {
            ApplyFrame(frames[playbackIndex]);
            playbackIndex++;
        }

        if (playbackIndex >= frames.Count)
        {
            StopPlayback();
        }
    }

    void ApplyFrame(RecordedFrame frame)
    {
        poseHandler.GetHumanPose(ref humanPose);

        for (int i = 0; i < humanPose.muscles.Length; i++)
        {
            humanPose.muscles[i] = frame.muscles[i];
        }

        poseHandler.SetHumanPose(ref humanPose);
    }

    // =======================
    // JSON I/O
    // =======================

    void SaveToJson()
    {
        float avgLatency = (latencyCount > 0) ? totalLatencySum / latencyCount : 0f;

        RecordingData data = new RecordingData
        {
            frames = frames,
            frameCount = frames.Count,
            duration = frames[frames.Count - 1].timestamp,
            recordedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

            // 🔥 遅延統計も一緒に保存
            avgLatency = avgLatency,
            minLatency = minLatency,
            maxLatency = maxLatency,
            latencySamples = latencyCount
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string path = Path.Combine(Application.dataPath, fileName + ".json");
        File.WriteAllText(path, json);

        Debug.Log($"💾 Saved: {path}");
        Debug.Log($"📊 Latency Avg={avgLatency:F1} / Min={minLatency:F1} / Max={maxLatency:F1}");
    }

    void LoadFromJson()
    {
        string path = Path.Combine(Application.dataPath, fileName + ".json");

        if (!File.Exists(path))
        {
            Debug.LogError("❌ Recording file not found");
            return;
        }

        string json = File.ReadAllText(path);
        RecordingData data = JsonConvert.DeserializeObject<RecordingData>(json);

        frames = data.frames;

        Debug.Log($"📊 Loaded Latency: Avg={data.avgLatency:F1} / Min={data.minLatency:F1} / Max={data.maxLatency:F1}");
    }

    // =======================
    // GUI 表示（任意だが超おすすめ）
    // =======================

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;

        if (isRecording)
        {
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(10, 10, 350, 30), "🔴 BODY + LATENCY REC", style);

            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 40, 300, 25), $"Current: {currentLatency:F1} ms", style);

            if (latencyCount > 0)
            {
                float avg = totalLatencySum / latencyCount;

                GUI.Label(new Rect(10, 70, 300, 25), $"Avg: {avg:F1} ms", style);
                GUI.Label(new Rect(10, 100, 300, 25), $"Min: {minLatency:F1} ms", style);
                GUI.Label(new Rect(10, 130, 300, 25), $"Max: {maxLatency:F1} ms", style);
                GUI.Label(new Rect(10, 160, 300, 25), $"Samples: {latencyCount}", style);
            }
        }
        else
        {
            style.normal.textColor = Color.gray;
            GUI.Label(new Rect(10, 10, 350, 30), "R : Record + Latency / P : Playback", style);
        }
    }
}
