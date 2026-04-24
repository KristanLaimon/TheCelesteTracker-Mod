using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Celeste.Mod.TheCelesteTracker_Mod.Source.services
{
    /// <summary>
    /// Handles WebSocket communication between the Celeste Mod and external tracking applications.
    /// </summary>
    public static class TrackerWebSocketServer
    {
        private const string LOG_TAG = "TheCelesteTracker_WS";
        private const int START_PORT = 50500;
        private const int MAX_PORT_ATTEMPTS = 100;

        private static HttpListener? _listener;
        private static readonly ConcurrentDictionary<WebSocket, byte> _clients = new();
        private static CancellationTokenSource? _cts;

        public static void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            
            if (TryStartServer(out int boundPort))
            {
                Logger.Log(LogLevel.Info, LOG_TAG, $"Server active at ws://localhost:{boundPort}/");
                Task.Run(() => ListenLoop(_cts.Token));
            }
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;

            _listener?.Stop();
            _listener?.Close();
            _listener = null;

            foreach (var client in _clients.Keys)
            {
                CleanupClient(client);
            }
            _clients.Clear();
        }

        private static bool TryStartServer(out int port)
        {
            for (int i = 0; i < MAX_PORT_ATTEMPTS; i++)
            {
                port = START_PORT + i;
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{port}/");
                    _listener.Start();
                    return true;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 32) // Port in use
                {
                    _listener?.Close();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, LOG_TAG, $"Critical failure during startup: {ex.Message}");
                    break;
                }
            }

            port = -1;
            return false;
        }

        private static async Task ListenLoop(CancellationToken token)
        {
            while (_listener != null && !token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketRequest(context, token);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    Logger.Log(LogLevel.Warn, LOG_TAG, $"Connection error: {ex.Message}");
                }
            }
        }

        private static async Task ProcessWebSocketRequest(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                var socket = wsContext.WebSocket;

                _clients.TryAdd(socket, 0);
                Logger.Log(LogLevel.Info, LOG_TAG, "Client connected.");

                await SendHandshake(socket);
                await KeepAliveClient(socket, token);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Verbose, LOG_TAG, $"Handshake failed: {ex.Message}");
            }
        }

        private static async Task SendHandshake(WebSocket socket)
        {
            await SendToClient(socket, new
            {
                Type = "DatabaseLocation",
                DatabasePath = TheCelesteTracker_ModModule.DbPath,
                EverestVersion = Everest.Version.ToString(),
                ModVersion = TheCelesteTracker_ModModule.Instance.Metadata.Version.ToString()
            });
        }

        private static async Task KeepAliveClient(WebSocket socket, CancellationToken token)
        {
            var buffer = new byte[1024];
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Verbose, LOG_TAG, $"Client dropped: {ex.Message}");
            }
            finally
            {
                CleanupClient(socket);
            }
        }

        private static void CleanupClient(WebSocket socket)
        {
            if (_clients.TryRemove(socket, out _))
            {
                try { socket.Dispose(); } catch { }
                Logger.Log(LogLevel.Info, LOG_TAG, "Client disconnected.");
            }
        }

        public static async Task BroadcastEvent(object payload)
        {
            var buffer = SerializeToBuffer(payload);
            foreach (var client in _clients.Keys)
            {
                await SendBuffer(client, buffer);
            }
        }

        private static async Task SendToClient(WebSocket socket, object payload)
        {
            await SendBuffer(socket, SerializeToBuffer(payload));
        }

        private static ArraySegment<byte> SerializeToBuffer(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            return new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        }

        private static async Task SendBuffer(WebSocket socket, ArraySegment<byte> buffer)
        {
            if (socket.State != WebSocketState.Open) return;

            try
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Verbose, LOG_TAG, $"Send failed: {ex.Message}");
            }
        }
    }
}