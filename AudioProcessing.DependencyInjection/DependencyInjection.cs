using AudioProcessing.Domain.Interfaces;
using AudioProcessing.Infrastructure.SharedMemory;
using AudioProcessing.Processes;
using AudioProcessing.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AudioProcessing.DependencyInjection
{
    public static class DependencyInjection
    {
        public static void AddInfrastructure(this IServiceCollection services)
        {
            // Регистрируем SharedMemoryManager как синглтон
            services.AddSingleton<ISharedMemoryManager, SharedMemoryManager>();
            
            // Регистрируем ProcessManager как синглтон
            services.AddSingleton<IProcessManager, ProcessManager>();
            
            // Регистрируем SessionManager как синглтон
            services.AddSingleton<ISessionManager, SessionManager>();
        }
    }
}