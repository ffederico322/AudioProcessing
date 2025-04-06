using AudioProcessing.Domain.Entity;
using AudioProcessing.Domain.Enum;

namespace AudioProcessing.Receiver.Utils
{
    public class MessageSerializer
    {
        public byte[] Serialize(AudioMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);
            
            // Записываем тип сообщения
            writer.Write((int)message.Type);
            
            // Записываем идентификатор сессии
            writer.Write(message.SessionId ?? string.Empty);
            
            // Записываем временную метку
            writer.Write(message.Timestamp);
            
            // Записываем размер данных
            int dataLength = message.Data?.Length ?? 0;
            writer.Write(dataLength);
            
            // Записываем данные, если они есть
            if (dataLength > 0)
            {
                writer.Write(message.Data);
            }
            
            writer.Flush();
            return memoryStream.ToArray();
        }

        public AudioMessage? Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            using var memoryStream = new MemoryStream(data);
            using var reader = new BinaryReader(memoryStream);
    
            try
            {
                // Считываем тип сообщения
                var type = (MessageType)reader.ReadInt32();
        
                // Считываем идентификатор сессии
                string sessionId = reader.ReadString();
        
                // Считываем временную метку
                long timestamp = reader.ReadInt64();
        
                // Считываем размер данных
                int dataLength = reader.ReadInt32();
        
                // Считываем данные, если они есть
                byte[] messageData = null;
                if (dataLength > 0)
                {
                    messageData = reader.ReadBytes(dataLength);
                }
        
                // Используем конструктор вместо инициализатора свойств
                return new AudioMessage(type, sessionId, messageData, timestamp);
            }
            catch (Exception)
            {
                // Если возникла ошибка при десериализации, возвращаем null
                return null;
            }
        }
    }
}