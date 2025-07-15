using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;

using UnityEngine.Animations;




public class toransform_muscle : MonoBehaviour
{
    public WebSocketConnection _connection;
    private string _url = "ws://localhost:8765";
    private bool _shouldReconnect = true;
    private CancellationTokenSource _cts;

    private HumanPoseHandler poseHandler;
    public HumanPose pose;

    public GameObject model;
    

    public Animator animator1;


    private void Start()
    {
        _cts = new CancellationTokenSource();

        _connection = gameObject.AddComponent<WebSocketConnection>();
        _connection.DesiredConfig = new WebSocketConfig { Url = _url };
        _connection.Connect();
        _connection.StateChanged += OnStateChanged;
        _connection.MessageReceived += OnMessageReceived;
        _connection.ErrorMessageReceived += OnErrorMessageReceived;

        SendMessagesPeriodically(_cts.Token).Forget();

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
        pose.muscles[42] = -1.0f;
        poseHandler.SetHumanPose(ref pose);

        Debug.Log("✅ poseHandler 初期化成功、muscle[42] を 0.5 に設定しました");
    }

    private void OnStateChanged(WebSocketConnection connection, WebSocketState oldState, WebSocketState newState)
    {
        Debug.Log($"WebSocket state changed from {oldState} to {newState}");
        if (newState == WebSocketState.Disconnected && _shouldReconnect)
        {
            Reconnect().Forget();
        }
    }

    private void OnMessageReceived(WebSocketConnection connection, WebSocketMessage message)
    {
        Debug.Log($"Raw JSON from server: {message.String}");
        


        try
        {
            var data = JsonConvert.DeserializeObject<BodyData>(message.String);
            Debug.Log($"Raw JSON from server: {data.bodys.Count}");
            //if (data != null && data.bodys != null && data.bodys.Count >= 14)
            if (data != null && data.bodys != null)
            {
                Vector3[] landmarks = new Vector3[10];
                for (int i = 0; i < 10; i++)
                {
                    landmarks[i] = new Vector3(data.bodys[i].x - 0.5f, -data.bodys[i].y + 1.5f, data.bodys[i].z);
                    //IKTransform[i].position = landmarks[i];
                }
                Quaternion upperArmRotation = GetBoneRotation(landmarks[2], landmarks[3]); // ← 前述の関数を使う

                

            }
            else
            {
                Debug.LogWarning("Received JSON is null or does not contain enough landmarks.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON parsing failed: {ex.Message}");
        }
    }


    private void OnErrorMessageReceived(WebSocketConnection connection, string errorMessage)
    {
        Debug.LogError($"WebSocket Error: {errorMessage}");
    }

    private async UniTaskVoid SendMessagesPeriodically(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_connection != null && _connection.State == WebSocketState.Connected)
            {
                var message = "Ping from Unity";
                _connection.AddOutgoingMessage(message);
                Debug.Log($"Message sent to server: {message}");
            }
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
        }
    }

    private async UniTaskVoid Reconnect()
    {
        Debug.Log("Attempting to reconnect...");
        await UniTask.Delay(TimeSpan.FromSeconds(5));
        if (_connection != null && _connection.State != WebSocketState.Connected)
        {
            _connection.Connect();
        }
    }

    private void OnDestroy()
    {
        _cts.Cancel();
        _cts.Dispose();
        _shouldReconnect = false;
        if (_connection != null)
        {
            _connection.Disconnect();
            _connection = null;
        }
    }
    Quaternion GetBoneRotation(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        if (direction == Vector3.zero) return Quaternion.identity;
        return Quaternion.LookRotation(direction);
    }


    [Serializable]
    public class BodyData
    {
        public List<ReceivedJson> bodys;
    }

    [Serializable]
    public class ReceivedJson
    {
        public float x;
        public float y;
        public float z;
    }
}
