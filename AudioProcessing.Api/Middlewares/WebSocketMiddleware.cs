using AudioProcessing.Api.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessing.Api.Middlewares
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly WebSocketHandler _webSocketHandler;
        private readonly ILogger<WebSocketMiddleware> _logger;

        public WebSocketMiddleware(
            RequestDelegate next,
            WebSocketHandler webSocketHandler,
            ILogger<WebSocketMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _webSocketHandler = webSocketHandler ?? throw new ArgumentNullException(nameof(webSocketHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Проверяем, является ли запрос WebSocket запросом к нашему маршруту
            if (context.Request.Path == "/audio")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    _logger.LogInformation("Обработка входящего WebSocket-запроса");
                    
                    // Принимаем WebSocket-подключение
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    
                    // Передаем управление в WebSocketHandler
                    await _webSocketHandler.HandleWebSocketAsync(webSocket, context.RequestAborted);
                }
                else
                {
                    _logger.LogWarning("Получен не-WebSocket запрос к маршруту /audio");
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                // Продолжаем обработку запроса другими middleware
                await _next(context);
            }
        }
    }
}