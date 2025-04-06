using AudioProcessing.Domain.Enum;
using Newtonsoft.Json;
using System;
using System.Text;

namespace AudioProcessing.Domain.Entity;

// Сделаем класс неизменяемым для безопасности при использовании в многопоточной среде
public class AudioMessage
{
    // Конструктор для создания сообщения
    public AudioMessage(MessageType type, string sessionId, byte[]? data, long timestamp)
    {
        Type = type;
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        Data = data ?? Array.Empty<byte>();
        Timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    
    // Фабричные методы для создания специализированных сообщений
    public static AudioMessage CreateActivate(string sessionId) => 
        new(MessageType.Activate, sessionId, null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    
    public static AudioMessage CreateAudioData(string sessionId, byte[] data) => 
        new(MessageType.AudioData, sessionId, data, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    
    public static AudioMessage CreateStop(string sessionId) => 
        new(MessageType.Stop, sessionId, null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    
    public static AudioMessage CreateError(string sessionId, string errorMessage) => 
        new(MessageType.Error, sessionId, 
            Encoding.UTF8.GetBytes(errorMessage), 
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    
    // Свойства только для чтения
    public MessageType Type { get; }
    public string SessionId { get; }
    public byte[]? Data { get; }
    public long Timestamp { get; }
    
    // Дополнительные свойства для удобства
    public string? GetErrorMessage() => 
        Type == MessageType.Error ? Encoding.UTF8.GetString(Data) : null;

    // Метод для сериализации в байты для передачи через SharedMemory
    public byte[] Serialize()
    {
        // Сериализация объекта в строку JSON
        string jsonString = JsonConvert.SerializeObject(this);

        // Преобразование строки JSON в массив байтов
        return Encoding.UTF8.GetBytes(jsonString);
    }

    public static AudioMessage? Deserialize(byte[] data)
    {
        var res = Encoding.UTF8.GetString(data);

        return JsonConvert.DeserializeObject<AudioMessage>(res);
    }
}

