namespace AudioProcessing.Domain.Interfaces;

public interface ISessionManager
{
    Task<string> CreateSessionAsync();
    Task RemoveSessionAsync(string sessionId);
    Task ActivateReceiverAsync(string sessionId);
    Task<IReadOnlyCollection<string>> GetActiveSessionsAsync();
}