namespace Wyrdrasil.Settlements.Tool;

public sealed class ZoneValidationDiagnostic
{
    public ZoneValidationDiagnosticCode Code { get; }
    public string Message { get; }

    public ZoneValidationDiagnostic(ZoneValidationDiagnosticCode code, string message)
    {
        Code = code;
        Message = message;
    }
}
