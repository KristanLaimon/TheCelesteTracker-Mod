using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public static class TrackerWebSocketServer
    {
        private static HttpListener _listener;
        private static readonly ConcurrentBag<WebSocket> _clients = new ConcurrentBag<WebSocket>();
        private static CancellationTokenSource _cts;

        public static void Start()
        {
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
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"WebSocket Start Error: {ex}");
                    break;
                }
            }

            if (started)
            {
                Task.Run(() => AcceptConnections(_cts.Token));
            }
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();

            foreach (var client in _clients)
            {
                client.Dispose();
            }
        }

        private static async Task AcceptConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                        _clients.Add(wsContext.WebSocket);
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
                    // Just keep the connection alive and wait for client to close
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
                ws.Dispose();
            }
        }

        public static async Task BroadcastEvent(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            List<WebSocket> toRemove = new List<WebSocket>();

            foreach (var client in _clients)
            {
                if (client.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        toRemove.Add(client);
                    }
                }
                else
                {
                    toRemove.Add(client);
                }
            }

            // Cleanup disconnected clients (ignoring race conditions for simplicity in this mod context)
            // In a production app, we'd use a more robust collection management.
        }
    }
}