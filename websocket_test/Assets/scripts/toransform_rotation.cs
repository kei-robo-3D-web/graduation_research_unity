using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;

public class toransform_rotation : MonoBehaviour
{
    public WebSocketConnection _connection;
    private string _url = "ws://localhost:8765";
    private bool _shouldReconnect = true;
    private CancellationTokenSource _cts;

    public GameObject[] hand = new GameObject[21];
    Transform[] handTransform = new Transform[21];

    HandData receivedJson;

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
        Debug.Log($"Raw JSON from server: {message.String}");

        try
        {
            var data = JsonConvert.DeserializeObject<HandData>(message.String);
            if (data != null && data.hands != null && data.hands.Count >= 21)
            {
                Vector3[] landmarks = new Vector3[21];
                for (int i = 0; i < 21; i++)
                {
                    landmarks[i] = new Vector3(data.hands[i].x, -data.hands[i].y, data.hands[i].z);
                }

                // l·‚µŽw‚Ì‰ñ“]“K—p (5¨6, 6¨7, 7¨8)
                ApplyBoneRotation(5, 6, landmarks);
                ApplyBoneRotation(6, 7, landmarks);
                ApplyBoneRotation(7, 8, landmarks);

                // Žè‚Ì‚Ð‚ç‰ñ“] (0, 5, 17)
                Vector3 wrist = landmarks[0];
                Vector3 indexBase = landmarks[5];
                Vector3 pinkyBase = landmarks[17];
                Vector3 dir1 = (indexBase - wrist).normalized;
                Vector3 dir2 = (pinkyBase - wrist).normalized;
                Vector3 palmNormal = Vector3.Cross(dir1, dir2).normalized;

                handTransform[0].rotation = Quaternion.LookRotation(palmNormal, dir1);
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

    private void ApplyBoneRotation(int from, int to, Vector3[] landmarks)
    {
        Vector3 dir = (landmarks[to] - landmarks[from]).normalized;
        dir.y = -dir.y;
        dir.x = -dir.x;
        //dir.x = dir.x + 90f;
        handTransform[from].rotation = Quaternion.LookRotation(dir);
        //handTransform[from].rotation.x += handTransform[0].rotation.x;
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
