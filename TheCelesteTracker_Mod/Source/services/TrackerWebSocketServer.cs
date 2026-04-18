using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Celeste.Mod.TheCelesteTracker_Mod.Source.services
{
    public static class TrackerWebSocketServer
    {
        private static HttpListener? _listener;
        private static readonly List<WebSocket> _clients = new List<WebSocket>();
        private static readonly object _clientsLock = new object();
        private static CancellationTokenSource? _cts;

        public static void Start()
        {
            Stop(); // Ensure clean state
            _cts = new CancellationTokenSource();
            int port = 50500;
            bool started = false;

            while (!started && port < 50600)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{port}/");
                    _listener.Start();
                    started = true;
                    Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", $"WebSocket server started on ws://localhost:{port}/");
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 32)
                {
                    port++;
                    _listener?.Close();
                    _listener = null;
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"WebSocket Start Error: {ex}");
                    break;
                }
            }

            if (started && _listener != null)
            {
                Task.Run(() => AcceptConnections(_cts.Token));
            }
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    try { client.Dispose(); } catch { }
                }
                _clients.Clear();
            }
        }

        private static async Task AcceptConnections(CancellationToken token)
        {
            if (_listener == null) return;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                        lock (_clientsLock)
                        {
                            _clients.Add(wsContext.WebSocket);
                        }
                        Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", "New WebSocket client connected.");

                        // Send DB location and versions immediately
                        _ = SendToClient(wsContext.WebSocket, new
                        {
                            Type = "DatabaseLocation",
                            EverestVersion = Everest.Version.ToString(),
                            ModVersion = TheCelesteTracker_ModModule.Instance.Metadata.Version.ToString()
                        });

                        _ = HandleClient(wsContext.WebSocket, token);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"WebSocket Accept Error: {ex}");
                }
            }
        }

        private static async Task HandleClient(WebSocket ws, CancellationToken token)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                    }
                }
            }
            catch { }
            finally
            {
                lock (_clientsLock) { _clients.Remove(ws); }
                ws.Dispose();
                Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", "WebSocket client disconnected.");
            }
        }

        public static async Task BroadcastEvent(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            WebSocket[] activeClients;
            lock (_clientsLock)
            {
                activeClients = _clients.Where(c => c.State == WebSocketState.Open).ToArray();
            }

            if (activeClients.Length == 0) return;

            foreach (var client in activeClients)
            {
                await SendToClient(client, segment);
            }
        }

        private static async Task SendToClient(WebSocket client, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            await SendToClient(client, new ArraySegment<byte>(buffer));
        }

        private static async Task SendToClient(WebSocket client, ArraySegment<byte> segment)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Verbose, "TheCelesteTracker_Mod", $"Failed to send to client: {ex.Message}");
            }
        }
    }
}