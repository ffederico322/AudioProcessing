namespace AudioProcessing.SharedMemory.Interfaces;

public interface IBufferSynchronizer
{
    Task<IDisposable> AcquireLockAsync(string bufferName, TimeSpan timeout = default);
    void SignalDataAvailable(string bufferName);
    Task WaitForDataAsync(string bufferName, CancellationToken cancellationToken = default);
}