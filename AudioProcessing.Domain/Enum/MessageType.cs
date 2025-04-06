namespace AudioProcessing.Domain.Enum;

public enum MessageType
{
    Activate,
    AudioData,
    Stop,
    Error,
    Heartbeat, // Добавляем тип для проверки состояния
    Config     // Для передачи настроек аудио (частота дискретизации, формат и т.д.)
}