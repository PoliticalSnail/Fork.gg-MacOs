using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ForkCommon.ExtensionMethods;
using ForkCommon.Model.Notifications;
using ForkFrontend.Model;
using ForkFrontend.Model.Enums;
using Microsoft.Extensions.Logging;

namespace ForkFrontend.Logic.Services.Notifications
{
    public class NotificationService
    {
        public delegate void WebsocketStatusChangedHandler(WebsocketStatus newStatus);

        private const int BUFFER_SIZE = 2048;
        private readonly ILogger<NotificationService> _logger;
        private readonly Uri _webSocketUri = new("ws://127.0.0.1:35565");
        private ClientWebSocket? _webSocket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private WebsocketStatus _websocketStatus = WebsocketStatus.Disconnected;

        private List<INotificationHandler> RegisteredHandlers { get; }

        public event WebsocketStatusChangedHandler? WebsocketStatusChanged;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            RegisteredHandlers = new List<INotificationHandler>();
            _logger.LogInformation("NotificationService initialized");
        }

        private WebsocketStatus WebsocketStatus
        {
            get => _websocketStatus;
            set
            {
                _websocketStatus = value;
                WebsocketStatusChanged?.Invoke(value);
            }
        }

        public void Register<T>(Func<T, Task> handler) where T : AbstractNotification
        {
            if (RegisteredHandlers.FirstOrDefault(h => h.GetType() == typeof(NotificationHandler<T>)) is not NotificationHandler<T> notificationHandler)
            {
                notificationHandler = new NotificationHandler<T>();
                RegisteredHandlers.Add(notificationHandler);
            }
            notificationHandler.Handlers.Add(handler);
            _logger.LogDebug($"Registered handler '{handler.Method.Name}' for {typeof(T)}");
        }

        public void Unregister<T>(object caller) where T : AbstractNotification
        {
            if (RegisteredHandlers.FirstOrDefault(h => h.GetType() == typeof(NotificationHandler<T>)) is NotificationHandler<T> notificationHandler)
            {
                int removed = notificationHandler.Handlers.RemoveAll(f => f.Target == caller);
                _logger.LogDebug($"Unregistered {removed} handlers for {typeof(T)}");
            }
        }

        public async Task StartupAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                _webSocket = new ClientWebSocket();
                try
                {
                    await foreach (var message in ConnectAsync(_cancellationTokenSource.Token))
                    {
                        await HandleMessage(message);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"WebSocket error: {e.Message}");
                }
                finally
                {
                    WebsocketStatus = WebsocketStatus.Disconnected;
                    _webSocket?.Abort();
                    _webSocket?.Dispose();
                    _logger.LogInformation("Reconnecting in 500ms...");
                    await Task.Delay(500);
                }
            }
        }

        private async Task HandleMessage(string message)
        {
            var notification = message.FromJson<AbstractNotification>();
            if (notification == null)
            {
                _logger.LogDebug("Received null notification, ignoring");
                return;
            }

            RegisteredHandlers.FirstOrDefault(h => h.Type == notification.GetType())?.CallHandlers(notification);
        }

        private async IAsyncEnumerable<string> ConnectAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_webSocket == null) yield break;

            _logger.LogInformation($"Connecting to {_webSocketUri}...");
            await _webSocket.ConnectAsync(_webSocketUri, cancellationToken);
            WebsocketStatus = WebsocketStatus.Connected;
            _logger.LogInformation("WebSocket connected");

            // Send a dummy token immediately
            await SendMessageAsync("dummyToken", cancellationToken);

            var buffer = new ArraySegment<byte>(new byte[BUFFER_SIZE]);
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                await using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    Debug.Assert(buffer.Array != null);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                yield return Encoding.UTF8.GetString(ms.ToArray());

                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }

        private async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                _logger.LogError("WebSocket is not connected, cannot send message");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            for (int i = 0; i < bytes.Length; i += BUFFER_SIZE)
            {
                var chunk = new ArraySegment<byte>(bytes.Skip(i).Take(BUFFER_SIZE).ToArray());
                await _webSocket.SendAsync(chunk, WebSocketMessageType.Text, i + BUFFER_SIZE >= bytes.Length, cancellationToken);
            }
            _logger.LogDebug($"Sent message: {message}");
        }
    }
}
