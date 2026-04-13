using BepInEx.Logging;

namespace Wyrdrasil.Construction.Diagnostics;

public sealed class ConstructionDebugLogService
{
    private readonly ManualLogSource _log;
    private readonly ConstructionDebugStateService _debugStateService;

    public ConstructionDebugLogService(ManualLogSource log, ConstructionDebugStateService debugStateService)
    {
        _log = log;
        _debugStateService = debugStateService;
    }

    public void Info(string scope, string message) => _log.LogInfo(Format(scope, message));
    public void Warning(string scope, string message) => _log.LogWarning(Format(scope, message));
    public void Error(string scope, string message) => _log.LogError(Format(scope, message));

    public void Verbose(string scope, string message)
    {
        if (!_debugStateService.Current.VerboseLoggingEnabled)
        {
            return;
        }

        _log.LogInfo(Format(scope, message));
    }

    private static string Format(string scope, string message) => $"[Construction][{scope}] {message}";
}
