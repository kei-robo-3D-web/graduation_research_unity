using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;

using UnityEngine.Animations;

public class body_transform : MonoBehaviour
{

    private HumanPoseHandler poseHandler;
    public HumanPose pose;

    public GameObject model;


    public Animator animator1;


    private void Start()
    {
        if (model == null)
        {
            Debug.LogError("❌ modelがInspectorで設定されていません");
            return;
        }

        animator1 = model.GetComponent<Animator>();
        if (animator1 == null)
        {
            Debug.LogError("❌ Animatorが見つかりません");
            return;
        }

        if (animator1.avatar == null || !animator1.avatar.isHuman)
        {
            Debug.LogError("❌ Avatarが設定されていない、またはHumanoidでありません");
            return;
        }

        poseHandler = new HumanPoseHandler(animator1.avatar, animator1.transform);
        poseHandler.GetHumanPose(ref pose);
        //pose.muscles[42] = -1.0f;
        
    }

    public void Update()
    {
        Vector3 rotation = GetBoneRotation(Variable_Share.landmarks[2], Variable_Share.landmarks[3]);
        Debug.Log(rotation.y);

        pose.muscles[42] = rotation.x;
        //pose.muscles[42] = -1.0f;
        poseHandler.SetHumanPose(ref pose);

    }




    Vector3 GetBoneRotation(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        Vector3 rotation;

        rotation.x = Mathf.Atan2(direction.y,direction.z);
        rotation.y = Mathf.Atan2(direction.x,direction.z);
        rotation.z = Mathf.Atan2(direction.x, direction.y);
        return rotation;
    }
}
