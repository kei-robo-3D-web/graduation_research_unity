using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using MikeSchweitzer.WebSocket;
using Random = UnityEngine.Random;
//using System.Net.WebSockets;
using Unity.VisualScripting;
using UnityEditor.VersionControl;
using Newtonsoft.Json;
using System.Collections.Generic;



public class test : MonoBehaviour
{
    public WebSocketConnection _connection;
    private string _url = "ws://localhost:8765";  // Pythonサーバーのアドレス
    private bool _shouldReconnect = true;
    private CancellationTokenSource _cts;

    public GameObject[] hand = new GameObject[20];
    Transform[] handTransform = new Transform[20];
    int landmarkCount = 0;
    //ReceivedJson receivedJson;

    HandData receivedJson;
    HandData inputJson;
    private void Start()
    {
        //handTransform = hand.transform;

        _cts = new CancellationTokenSource();

        // WebSocketの初期化と接続
        _connection = gameObject.AddComponent<WebSocketConnection>();
        _connection.DesiredConfig = new WebSocketConfig
        {
            Url = _url
        };

        _connection.Connect();

        // 接続状態の変更イベント
        _connection.StateChanged += OnStateChanged;

        // メッセージ受信イベント
        _connection.MessageReceived += OnMessageReceived;

        // エラーメッセージ
        _connection.ErrorMessageReceived += OnErrorMessageReceived;

        // 定期的なメッセージ送信を非同期タスクで実行
        SendMessagesPeriodically(_cts.Token).Forget();

        for (int i = 0; i < 20; i++) {
            if (i != 4 && i != 8 && i != 12 && i != 16 && i != 20) {
                handTransform[i] = hand[i].transform;
            }

        }
        

    }

    private void OnStateChanged(WebSocketConnection connection, WebSocketState oldState, WebSocketState newState)
    {
        Debug.Log($"WebSocket state changed from {oldState} to {newState}");

        // 再接続の試み
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
            if (data != null && data.hands != null)
            {
                foreach (var hand in data.hands)
                {
                    //Debug.Log(landmarkCount);
                    Debug.Log($"x: {hand.x}, y: {hand.y}, z: {hand.z}");

                    if (landmarkCount == 0 || landmarkCount == 1 || landmarkCount == 5 || landmarkCount == 9 || landmarkCount == 13 || landmarkCount == 17)
                    {
                        handTransform[landmarkCount].position = new Vector3(hand.x, -1 * hand.y + 0.002f, 0.1f * hand.z);
                    }
                    //if (landmarkCount == 0 || landmarkCount == 1 || landmarkCount == 5 || landmarkCount == 9 || landmarkCount == 13 || landmarkCount == 17)
                    //{
                    //    handTransform[landmarkCount].position = new Vector3(hand.x, -1 * hand.y + 0.002f, 0.1f * hand.z);
                    //}
                    //else if (landmarkCount != 4 && landmarkCount != 8 && landmarkCount != 12 && landmarkCount != 16 && landmarkCount != 20)
                    //{
                    //    handTransform[landmarkCount].position = new Vector3( 0.001f * hand.x, 0.001f * hand.y, 0.001f * hand.z);
                    //    Debug.Log(landmarkCount);
                    //}

                    else if (landmarkCount == 1) {
                        handTransform[landmarkCount].localPosition = new Vector3(hand.x, -1 * hand.y + 0.002f, hand.z);
                    }
                        landmarkCount++;
                    
                }
                landmarkCount = 0;
            }
            else
            {
                Debug.LogWarning("Received JSON is null or does not contain 'hands' field.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON parsing failed: {ex.Message}");
        }
    }

    private void OnErrorMessageReceived(WebSocketConnection connection, string errorMessage)
    {
        
    }

    private async UniTaskVoid SendMessagesPeriodically(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_connection != null && _connection.State == WebSocketState.Connected)
            {
                var message = "Random message: " + Random.Range(0, 1000);
                _connection.AddOutgoingMessage(message);
                Debug.Log($"Message sent to server: {message}");
            }

            // 送信間隔を待機
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