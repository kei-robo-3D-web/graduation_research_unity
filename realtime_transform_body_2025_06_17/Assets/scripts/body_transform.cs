using System;
using UnityEngine;
using System.IO;

public class BodyTransform : MonoBehaviour
{
    private HumanPoseHandler poseHandler;
    private HumanPose pose;

    [Header("Model")]
    public GameObject model;
    private Animator animator;

    [Header("Tuning (XY)")]
    [Range(0f, 2f)] public float shoulderUpDownGain = 1.0f;
    [Range(0f, 2f)] public float shoulderFrontBackGain = 1.0f;
    [Range(0f, 2f)] public float elbowGain = 1.0f;

    [Header("Depth (MediaPipe Z)")]
    [Range(0f, 2f)] public float depthGain = 0.6f;
    [Range(0f, 0.5f)] public float depthClamp = 0.25f;

    [Header("Depth → Arm Down-Up")]
    [Range(0f, 1f)] public float depthToUpDownGain = 0.35f;

    [Header("Leg Tuning")]
    [Range(0f, 2f)] public float legUpDownGain = 1.0f;


    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothFactor = 0.15f;

    float[] smoothMuscles = new float[95];
    private string totalLatencyLogPath;


    void Start()
    {
        animator = model.GetComponent<Animator>();
        poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        poseHandler.GetHumanPose(ref pose);

        for (int i = 0; i < smoothMuscles.Length; i++)
            smoothMuscles[i] = pose.muscles[i];
        animator = model.GetComponent<Animator>();
        poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        poseHandler.GetHumanPose(ref pose);

        for (int i = 0; i < smoothMuscles.Length; i++)
            smoothMuscles[i] = pose.muscles[i];

        // =========================
        // 🔥 総遅延ログ初期化
        // =========================
        totalLatencyLogPath = Path.Combine(
            Application.persistentDataPath,
            "total_latency_log.csv"
        );

        if (!File.Exists(totalLatencyLogPath))
        {
            File.WriteAllText(
                totalLatencyLogPath,
                "ApplyTime,SendTime,TotalLatencyMs\n"
            );
        }

        Debug.Log($"Total latency log path: {totalLatencyLogPath}");
    }

    void Update()
    {
        var lm = GetLandmarksFullBody.poseLandmarks;
        if (lm == null || lm.Length < 17) return;

        // =========================
        // 右腕
        // =========================
        ApplyArm(
            lm[12], lm[14], lm[16],transform.up,
            39, 40, 41 ,42,
            true
        );

        // =========================
        // 左腕
        // =========================
        ApplyArm(
            lm[11], lm[13], lm[15],transform.up,
            48, 49, 50, 51,
            false
        );

        // =========================
        // 右脚
        // =========================
        ApplyLeg(
            lm[24],  // RIGHT_HIP
            lm[26],  // RIGHT_KNEE
            lm[28],  // RIGHT_ANKLE
            29
        );

        // =========================
        // 左脚
        // =========================
        ApplyLeg(
            lm[23],  // LEFT_HIP
            lm[25],  // LEFT_KNEE
            lm[27],  // LEFT_ANKLE
            21
        );

        poseHandler.SetHumanPose(ref pose);
        double nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        double totalLatency = nowMs - GetLandmarksFullBody.lastReceivedTimestamp;

        Debug.Log($"Total latency (after apply): {totalLatency:F1} ms");

        // =========================
        // 🔥 ファイル保存
        // =========================
        string line =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
            $"{GetLandmarksFullBody.lastReceivedTimestamp}," +
            $"{totalLatency:F1}\n";

        File.AppendAllText(totalLatencyLogPath, line);
    }

    void ApplyArm(
        Vector3 shoulder,
        Vector3 elbow,
        Vector3 wrist,
        Vector3 bodyUp,
        int muscleArmUD,
        int muscleShoulderFB,
        int muscleArmTwist,
        int muscleElbow,
        bool isRight
    )
    {
        // =========================
        // 上腕方向（XY）
        // =========================
        Vector3 upperDir = (elbow - shoulder).normalized;

        // ===== 肩：前後（XY）=====
        float frontBackXY = Vector3.Dot(upperDir, transform.forward);
        smoothMuscles[muscleShoulderFB] = Mathf.Lerp(
                            smoothMuscles[muscleShoulderFB],
                            Mathf.Clamp(frontBackXY * shoulderFrontBackGain, -1f, 1f),
                            smoothFactor
                            );

        pose.muscles[muscleShoulderFB] = smoothMuscles[muscleShoulderFB];

        //pose.muscles[muscleShoulderFB] =
        //    Mathf.Clamp(frontBackXY * shoulderFrontBackGain, -1f, 1f);

        // ===== 肩：上下（XY）=====
        float upDownXY = -Vector3.Dot(upperDir, transform.up);
        smoothMuscles[muscleArmUD] = Mathf.Lerp(
                    smoothMuscles[muscleArmUD],
                    Mathf.Clamp(upDownXY * shoulderUpDownGain, -1f, 1f),
                    smoothFactor
                    );

        pose.muscles[muscleArmUD] = smoothMuscles[muscleArmUD];

        //pose.muscles[muscleArmUD] =
        //    Mathf.Clamp(upDownXY * shoulderUpDownGain, -1f, 1f);

        // =========================
        // 肘
        // =========================

        smoothMuscles[muscleElbow] = Mathf.Lerp(
                    smoothMuscles[muscleElbow],
                    -CalcElbowBend(shoulder, elbow, wrist) * elbowGain,
                    smoothFactor
                    );

        pose.muscles[muscleElbow] = smoothMuscles[muscleElbow];

        //pose.muscles[muscleElbow] =
        //    -CalcElbowBend(shoulder, elbow, wrist) * elbowGain;


        // =====腕：回転=====
        //不安定なので結局使ってません
        //Vector3 upper = (elbow - shoulder).normalized;
        //Vector3 lower = (wrist - elbow).normalized;

        //// 前腕を上腕に直交する平面へ射影
        //Vector3 lowerProj = Vector3.ProjectOnPlane(lower, upper).normalized;

        //// 基準軸（上腕×体の上）
        //Vector3 refAxis = Vector3.Cross(upper, bodyUp).normalized;

        //// 符号付き角度
        //float armTwist = Vector3.SignedAngle(refAxis, lowerProj, upper);

        //armTwist = Mathf.Clamp(armTwist, -60f, 60f);
        //armTwist *= 0.6f;


        //smoothMuscles[muscleArmTwist] = Mathf.Lerp(
        //            smoothMuscles[muscleArmTwist],
        //            -1 * Mathf.Clamp(armTwist / 90f, -1f, 1f),
        //            smoothFactor
        //            );

        //if (lowerProj.sqrMagnitude > 0.0001f)

        //    pose.muscles[muscleArmTwist] = smoothMuscles[muscleArmTwist];

        //pose.muscles[muscleArmTwist] = -1 * Mathf.Clamp(armTwist / 90f, -1f, 1f);



        // =================================================
        // 🔥 MediaPipe Z による奥行き補正
        // =================================================
        float depth = wrist.z - shoulder.z;          // 前に出すとマイナス
        depth = Mathf.Clamp(depth, -depthClamp, depthClamp);

        // 正規化（前 = +1）
        float depthValue = -depth / depthClamp;
        depthValue *= depthGain;

        // --- Front-Back に加算 ---
        pose.muscles[muscleShoulderFB] =
            Mathf.Clamp(
                pose.muscles[muscleShoulderFB] + depthValue,
                -1f, 1f
            );

        // --- 🔥 Arm Down-Up にも加算 ---
        pose.muscles[muscleArmUD] =
            -1 * 
            Mathf.Clamp(
                pose.muscles[muscleArmUD]
                - depthValue * depthToUpDownGain,
                -1f, 1f
            );
    }

    void ApplyLeg(
    Vector3 hip,
    Vector3 knee,
    Vector3 ankle,
    int muscleLegUD
)
    {
        // 太もも方向
        Vector3 thighDir = (knee - hip).normalized;

        // 上下（脚上げ）
        float upDown = -Vector3.Dot(thighDir, transform.up);

        smoothMuscles[muscleLegUD] = Mathf.Lerp(
            smoothMuscles[muscleLegUD],
            Mathf.Clamp(upDown * legUpDownGain, -1f, 1f),
            smoothFactor
        );

        pose.muscles[muscleLegUD] = smoothMuscles[muscleLegUD];
    }


    float CalcElbowBend(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        Vector3 upper = (shoulder - elbow).normalized;
        Vector3 lower = (wrist - elbow).normalized;

        float angle = Vector3.Angle(upper, lower);
        float t = Mathf.InverseLerp(180f, 60f, angle);
        return Mathf.Clamp(t * 2f - 1f, -1f, 1f);
    }
}
