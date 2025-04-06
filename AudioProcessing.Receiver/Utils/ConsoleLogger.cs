namespace AudioProcessing.Receiver.Utils
{
    public class ConsoleLogger
    {
        private readonly string _category;

        public ConsoleLogger(string category)
        {
            _category = category ?? throw new ArgumentNullException(nameof(category));
        }

        public void LogInformation(string message)
        {
            Log(message, ConsoleColor.Green, "INFO");
        }

        public void LogWarning(string message, int size)
        {
            Log(message, ConsoleColor.Yellow, "WARN");
        }

        public void LogError(string message)
        {
            Log(message, ConsoleColor.Red, "ERROR");
        }

        public void LogDebug(string message)
        {
            Log(message, ConsoleColor.Cyan, "DEBUG");
        }

        private void Log(string message, ConsoleColor color, string level)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{_category}] {message}");
            
            Console.ForegroundColor = originalColor;
        }

        public void LogWarning(string полученоNullСообщениеПропускаем)
        {
            throw new NotImplementedException();
        }
    }
}