namespace AudioProcessing.Domain.Interfaces;

public interface ISharedMemoryManager
{
    void RegisterBuffer(string bufferName, int size);
    void UnregisterBuffer(string bufferName);
    void Write(string bufferName, byte[] data, int offset, int count);
    byte[] Read(string bufferName, int offset, int count);
    Task<byte[]> WaitAndReadAsync(string bufferName, CancellationToken cancellationToken = default);
}