using UnityEngine;
using System;
using System.IO;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace YuJian.Network
{
    /// <summary>
    /// WebSocket客户端 - 连接Python手势引擎
    /// 使用System.Net.WebSockets（Unity 2021+内置支持）
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        private string _host;
        private int _port;
        private GestureDataBuffer _buffer;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private float _reconnectTimer;

        [Header("连接设置")]
        [SerializeField] private float reconnectInterval = 3f;
        [SerializeField] private int receiveBufferSize = 8192;

        /// <summary>是否已连接</summary>
        public bool IsConnected => _isConnected;

        public void Initialize(string host, int port, GestureDataBuffer buffer)
        {
            _host = host;
            _port = port;
            _buffer = buffer;
            Connect();
        }

        private async void Connect()
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            string url = $"ws://{_host}:{_port}";
            Debug.Log($"[WebSocket] 正在连接: {url}");

            try
            {
                await _ws.ConnectAsync(new Uri(url), _cts.Token);
                _isConnected = true;
                Debug.Log("[WebSocket] 连接成功");
                _ = ReceiveLoop();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocket] 连接失败: {e.Message}");
                _isConnected = false;
                _reconnectTimer = reconnectInterval;
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[receiveBufferSize];
            var segment = new ArraySegment<byte>(buffer);

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    // 使用MemoryStream累积分片消息
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _ws.ReceiveAsync(segment, _cts.Token);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                Debug.Log("[WebSocket] 服务端关闭连接");
                                return;
                            }

                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Binary ||
                            result.MessageType == WebSocketMessageType.Text)
                        {
                            byte[] data = ms.ToArray();

                            // 反序列化并写入缓冲区
                            var frame = MessageDeserializer.Deserialize(data);
                            if (frame != null)
                            {
                                _buffer.Write(frame);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocket] 接收错误: {e.Message}");
            }
            finally
            {
                _isConnected = false;
                _reconnectTimer = reconnectInterval;
            }
        }

        private void Update()
        {
            // 自动重连
            if (!_isConnected)
            {
                _reconnectTimer -= Time.deltaTime;
                if (_reconnectTimer <= 0)
                {
                    Reconnect();
                }
            }
        }

        private void Reconnect()
        {
            Disconnect();
            Connect();
        }

        private void Disconnect()
        {
            _cts?.Cancel();
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "Client closing", CancellationToken.None).Wait(1000);
                }
                catch { }
            }
            _ws?.Dispose();
            _ws = null;
            _isConnected = false;
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
