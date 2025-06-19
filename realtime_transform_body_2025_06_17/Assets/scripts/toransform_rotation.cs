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

    public GameObject[] body  = new GameObject[14];
    Transform[] bodyTransform = new Transform[14];

    BodyData receivedJson;

    public enum landmarks
    {
        sholderL,
        sholderR,
        elbowL,
        elbowR,
        handL,
        handR,
        hipL,
        hipR,
        kneeL,
        kneeR,
        ankleL,
        ankleR,
        footL,
        footR
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

        body[(int)landmarks.sholderL] = GameObject.Find("body.011");
        body[(int)landmarks.sholderR] = GameObject.Find("body.012");

        body[(int)landmarks.  elbowL] = GameObject.Find("body.013");
        body[(int)landmarks.  elbowR] = GameObject.Find("body.014");

        body[(int)landmarks.   handL] = GameObject.Find("body.015");
        body[(int)landmarks.   handR] = GameObject.Find("body.016");

        body[(int)landmarks.    hipL] = GameObject.Find("body.023");
        body[(int)landmarks.    hipR] = GameObject.Find("body.024");

        body[(int)landmarks.   kneeL] = GameObject.Find("body.025");
        body[(int)landmarks.   kneeR] = GameObject.Find("body.026");

        body[(int)landmarks.  ankleL] = GameObject.Find("body.027");
        body[(int)landmarks.  ankleR] = GameObject.Find("body.028");

        body[(int)landmarks.   footL] = GameObject.Find("body.027_end");
        body[(int)landmarks.   footR] = GameObject.Find("body.028_end");

        for (int i = 0; i < 14; i++)
        {
            bodyTransform[i] = body[i].transform;
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
        


        try
        {
            var data = JsonConvert.DeserializeObject<BodyData>(message.String);
            Debug.Log($"Raw JSON from server: {data.bodys.Count}");
            //if (data != null && data.bodys != null && data.bodys.Count >= 14)
            if (data != null && data.bodys != null)
                {
                Vector3[] landmarks = new Vector3[14];
                for (int i = 0; i < 14; i++)
                {
                    landmarks[i] = new Vector3(data.bodys[i].x, -data.bodys[i].y, data.bodys[i].z);
                    bodyTransform[i].position = landmarks[i];
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
