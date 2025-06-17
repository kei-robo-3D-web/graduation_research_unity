using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.VisualScripting;

public class toransform_finger_scale : MonoBehaviour
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

        for (int i = 0; i < 21; i++)
        {
            //if (i != 4 && i != 8 && i != 12 && i != 16 && i != 20) {
            //    handTransform[i] = hand[i].transform;
            //}

            if (i == 4 || i == 8 || i == 12 || i == 16 || i == 20)
            {

                if (i < 10 && i != 0)
                {
                    hand[i] = GameObject.Find("hand.00" + (i - 1).ToString() + "_end");
                    //Debug.Log("hand.00" + (i - 1).ToString() + "_end");
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
                    Debug.Log("hand.00" + (i).ToString());
                }
                else
                {
                    hand[i] = GameObject.Find("hand.0" + (i).ToString());
                }
            }


            handTransform[i] = hand[i].transform;
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
        //Debug.Log($"Raw JSON from server: {message.String}");


        var data = JsonConvert.DeserializeObject<HandData>(message.String);
        if (data != null && data.hands != null && data.hands.Count >= 21)
        {
            Vector3[] landmarks = new Vector3[21];
            for (int i = 0; i < 21; i++)
            {
                landmarks[i] = new Vector3(data.hands[i].x, -data.hands[i].y, data.hands[i].z);
            }

            euclid.x = (landmarks[8].x - landmarks[5].x);
            euclid.y = (landmarks[8].y - landmarks[5].y);
            euclid.z = (landmarks[8].z - landmarks[5].z - landmarks[0].z);

            euclidDistance = Vector3.Distance(landmarks[5], landmarks[0]);
            double distance = Math.Sqrt(Math.Pow(euclid.x,2) + Math.Pow(euclid.y, 2) + Math.Pow(euclid.z, 2));
            Debug.Log(euclidDistance);
            handTransform[0].position = new Vector3(landmarks[0].x, landmarks[0].y, landmarks[0].z + (euclidDistance * 20.0f));
            euclidDistance = 0.0f;
            //Debug.Log(landmarks[8]);
            //Debug.Log(landmarks[5]);

            euclid = new Vector3(0, 0, 0);

            // 人差し指の回転適用 (5→6, 6→7, 7→8)
            //ApplyBoneRotation(5, 6, landmarks);
            //ApplyBoneRotation(6, 7, landmarks);
            //ApplyBoneRotation(7, 8, landmarks);


            // 手のひら回転 (0, 5, 17)
            Vector3 wrist = landmarks[0];
            Vector3 indexBase = landmarks[5];
            Vector3 pinkyBase = landmarks[17];
            Vector3 dir1 = (indexBase - wrist).normalized;
            Vector3 dir2 = (pinkyBase - wrist).normalized;
            Vector3 palmNormal = Vector3.Cross(dir1, dir2).normalized;

            handTransform[0].rotation = Quaternion.LookRotation(palmNormal, dir1);

            finger_rotation(handTransform, landmarks, 1,  4);//人差し指
            finger_rotation(handTransform, landmarks, 5,  8);//人差し指
            finger_rotation(handTransform, landmarks, 9, 12);//中指
            finger_rotation(handTransform, landmarks, 13,16);//薬指
            finger_rotation(handTransform, landmarks, 17,20);//小指

            //handTransform[5].localRotation = Quaternion.Euler(0, 0, finger_rotation(landmarks, 5, 8));
            //handTransform[6].localRotation = Quaternion.Euler(0, 0, finger_rotation(landmarks, 5, 8));
            //handTransform[7].localRotation = Quaternion.Euler(0, 0, finger_rotation(landmarks, 5, 8));
            //handTransform[8].localRotation = Quaternion.Euler(0, 0, finger_rotation(landmarks, 5, 8));

        }
        else
        {
            Debug.LogWarning("Received JSON is null or does not contain enough landmarks.");
        }

    }

    private void ApplyBoneRotation(int from, int to, Vector3[] landmarks)
    {
        Vector3 dir = (landmarks[to] - landmarks[from]).normalized;
        dir.y = -dir.y;
        dir.x = -dir.x;
        //dir.x = dir.x + 90f;
        handTransform[from].rotation = Quaternion.LookRotation(dir);
        //handTransform[from].rotation.x += handTransform[0].rotation.x;
    }
    void finger_rotation(Transform[] handTransform,Vector3[] landmarks,int baseFinger,int end) {
        float straightDistance = Vector3.Distance(landmarks[baseFinger], landmarks[end]);
        float totalJointDistance = Vector3.Distance(landmarks[baseFinger],     landmarks[baseFinger + 1])
                                 + Vector3.Distance(landmarks[baseFinger + 1], landmarks[baseFinger + 2])
                                 + Vector3.Distance(landmarks[baseFinger + 2], landmarks[end]);
        float bendRatio = Mathf.Clamp01(straightDistance / totalJointDistance);
        float angle = (1f - bendRatio) * 90.0f;

        for (int i = baseFinger; i < end; i++) {
            handTransform[i].localRotation = Quaternion.Euler(0, 0, angle);
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
                //Debug.Log($"Message sent to server: {message}");
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
    public class HandData
    {
        public List<ReceivedJson> hands;
    }

    [Serializable]
    public class ReceivedJson
    {
        public float x;
        public float y;
        public float z;
    }
}
