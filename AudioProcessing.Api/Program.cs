using AudioProcessing.Api.Middlewares;
using AudioProcessing.Api.WebSockets;
using AudioProcessing.Application.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using AudioProcessing.DependencyInjection;
using Microsoft.AspNetCore.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Настройка сервисов
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Добавляем поддержку WebSockets
builder.Services.AddWebSockets(options =>
{
    // Максимальный размер сообщения - 64 КБ
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);

    // СТАРАЯ ЧАСТЬ КОДА, ЗАМЕНИ НА НОВУЮ
    options.ReceiveBufferSize = 64 * 1024;
});

// Регистрируем наши слои
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// Регистрируем WebSocketHandler
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

// Настройка HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Добавляем middleware для WebSockets
app.UseWebSockets();

// Добавляем наш middleware для обработки WebSocket запросов
app.UseMiddleware<AudioProcessing.Api.Middlewares.WebSocketMiddleware>();

// Настраиваем обработку обычных запросов
app.MapGet("/", () => "Audio Processing Server - WebSocket endpoint available at /audio");

app.Run();