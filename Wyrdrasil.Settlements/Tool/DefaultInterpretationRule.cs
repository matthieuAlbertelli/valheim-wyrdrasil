namespace Wyrdrasil.Settlements.Tool;

public sealed class DefaultInterpretationRule
{
    public ZoneDesignationKind SourceDesignationKind { get; }
    public ZoneInterpretationKind InterpretationKind { get; }

    public DefaultInterpretationRule(ZoneDesignationKind sourceDesignationKind, ZoneInterpretationKind interpretationKind)
    {
        SourceDesignationKind = sourceDesignationKind;
        InterpretationKind = interpretationKind;
    }
}
