namespace Wyrdrasil.Core.Persistence;

public interface IWorldPersistenceParticipant
{
    string ModuleId { get; }
    int SchemaVersion { get; }
    void ResetForWorldChange();
    string CapturePayload();
    void RestorePayload(string payloadXml);
    bool RetryDeferredResolutions();
}
