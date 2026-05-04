namespace Wyrdrasil.Core.Persistence;

public interface IWorldPersistenceRestoreHook
{
    void OnAfterRestore();

    void OnAfterDeferredResolutions();
}
