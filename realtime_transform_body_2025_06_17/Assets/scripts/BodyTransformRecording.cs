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

    [Header("Recording")]
    public bool isRecording = false;
    public bool isPlayback = false;
    public string fileName = "body_muscle_recording";

    private float recordStartTime;
    private float playbackStartTime;
    private int playbackIndex;

    private List<RecordedFrame> frames = new List<RecordedFrame>();

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
    }

    void Update()
    {
        // R : Record
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRecording)
                StartRecording();
            else
                StopRecording();
        }

        // P : Playback
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
        frames.Clear();
        recordStartTime = Time.time;
        isRecording = true;
        Debug.Log("🔴 Body Recording Start");
    }

    void StopRecording()
    {
        isRecording = false;
        SaveToJson();
        Debug.Log($"⏹ Recording Stop ({frames.Count} frames)");
    }

    void RecordFrame()
    {
        poseHandler.GetHumanPose(ref humanPose);

        RecordedFrame frame = new RecordedFrame
        {
            timestamp = Time.time - recordStartTime,
            muscles = new List<float>(humanPose.muscles) // 🔥 musclesはそのまま
        };

        frames.Add(frame);
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

        // 🔥 muscles 部分は変更しない（値をそのまま戻すだけ）
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
        RecordingData data = new RecordingData
        {
            frames = frames,
            frameCount = frames.Count,
            duration = frames[frames.Count - 1].timestamp,
            recordedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string path = Path.Combine(Application.dataPath, fileName + ".json");
        File.WriteAllText(path, json);

        Debug.Log($"💾 Saved: {path}");
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
    }
}
