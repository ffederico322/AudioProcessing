namespace AudioProcessing.Application.Interfaces;

public interface IAudioSessionService
{
    Task<string> CreateAudioSessionAsync();
    Task CloseAudioSessionAsync(string sessionId);
    Task ActivateReceiverAsync(string sessionId);

    // Поменять на bool
    Task SendAudioToReceiverAsync(string sessionId, byte[] audioData);
}