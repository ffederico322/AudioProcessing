using AudioProcessing.Application.Interfaces;
using AudioProcessing.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AudioProcessing.Application.DependencyInjection
{
    public static class DependencyInjection
    {
        public static void AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAudioSessionService, AudioSessionService>();
        }
    }
}