using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine.XR;

public class toransform_rotation : MonoBehaviour
{
    public WebSocketConnection _connection;
    private string _url = "ws://localhost:8765";
    private bool _shouldReconnect = true;
    private CancellationTokenSource _cts;

    BodyData receivedJson;
    float euclidDistance = 0.0f;

    float currentDistance = 0.0f;
    float desiredDistance = 0.5f;
    float scaleFactor = 0.0f;

    float baseDepth = 0.75f;
    float depth = 0.0f;
    float scale = 1.5f;
    Vector3 midlePoint;

    public GameObject[] IKObject = new GameObject[8];
    public Transform[] IKTransform = new Transform[8];

    public GameObject model;
    public Transform modelTransform;
    public enum IKtarget
    {
        elbowR,
        elbowL,
        handR,
        handL,
        footR,
        footL,
        kneeR,
        kneeL
    }

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

        IKObject[(int)IKtarget.handR] = GameObject.Find("RightHandTarget");
        IKObject[(int)IKtarget.handL] = GameObject.Find("LeftHandTarget");

        IKObject[(int)IKtarget.elbowR] = GameObject.Find("RightHintElbow");
        IKObject[(int)IKtarget.elbowL] = GameObject.Find("LeftHintElbow");

        IKObject[(int)IKtarget.footR] = GameObject.Find("RightFootTarget");
        IKObject[(int)IKtarget.footL] = GameObject.Find("LeftFootTarget");

        IKObject[(int)IKtarget.kneeR] = GameObject.Find("RightHintKnee");
        IKObject[(int)IKtarget.kneeL] = GameObject.Find("LeftHintKnee");

        modelTransform = model.transform;

        for (int i = 0; i < 8; i++)
        {
            IKTransform[i] = IKObject[i].transform;
        }
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


                euclidDistance = Vector3.Distance(landmarks[3], landmarks[0]);
                midlePoint = Vector3.Lerp(landmarks[0],landmarks[1], 0.5f);

                currentDistance = Vector3.Distance(landmarks[0], landmarks[4]);
                scaleFactor = desiredDistance / Mathf.Max(0.01f, currentDistance);
                //for (int i = 0; i < landmarks.Length; i++)
                //{
                //    landmarks[i] = (landmarks[i] - landmarks[0]) * scaleFactor + landmarks[0];
                //}

                depth = Mathf.Abs(landmarks[0].z);
                //scale = baseDepth / Mathf.Max(0.001f, depth);

                //modelTransform.position = new Vector3(midlePoint.x, midlePoint.y, -(midlePoint.z + (euclidDistance * 20.0f)));
                modelTransform.position = new Vector3(midlePoint.x, midlePoint.y, -(midlePoint.z));


                for (int i = 0; i < 8; i++)
                {
                    //IKTransform[i].position = new Vector3(landmarks[i + 2].x, landmarks[i + 2].y, -(landmarks[i + 2].z));
                    IKTransform[i].position = new Vector3(landmarks[i + 2].x * scale, landmarks[i + 2].y * scale, -(landmarks[i + 2].z + 0.5f));
                }
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
