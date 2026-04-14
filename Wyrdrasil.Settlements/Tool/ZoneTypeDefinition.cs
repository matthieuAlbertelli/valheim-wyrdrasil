using System;
using System.Collections.Generic;
using System.Linq;

namespace Wyrdrasil.Settlements.Tool;

public sealed class ZoneTypeDefinition
{
    private readonly HashSet<ZoneDesignationKind> _supportedDesignations;
    private readonly List<DefaultInterpretationRule> _defaultInterpretationRules;

    public ZoneType ZoneType { get; }
    public string DisplayName { get; }
    public ZoneCapability Capabilities { get; }
    public IReadOnlyCollection<ZoneDesignationKind> SupportedDesignations => _supportedDesignations;
    public IReadOnlyList<DefaultInterpretationRule> DefaultInterpretationRules => _defaultInterpretationRules;
    public ZoneRequirementProfile ConfiguredRequirements { get; }
    public ZoneRequirementProfile StaffedRequirements { get; }
    public ZoneRequirementProfile OperationalRequirements { get; }

    public ZoneTypeDefinition(
        ZoneType zoneType,
        string displayName,
        ZoneCapability capabilities,
        IEnumerable<ZoneDesignationKind> supportedDesignations,
        IEnumerable<DefaultInterpretationRule> defaultInterpretationRules,
        ZoneRequirementProfile configuredRequirements,
        ZoneRequirementProfile staffedRequirements,
        ZoneRequirementProfile operationalRequirements)
    {
        ZoneType = zoneType;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Capabilities = capabilities;
        _supportedDesignations = new HashSet<ZoneDesignationKind>(supportedDesignations ?? Enumerable.Empty<ZoneDesignationKind>());
        _defaultInterpretationRules = new List<DefaultInterpretationRule>(defaultInterpretationRules ?? Enumerable.Empty<DefaultInterpretationRule>());
        ConfiguredRequirements = configuredRequirements ?? throw new ArgumentNullException(nameof(configuredRequirements));
        StaffedRequirements = staffedRequirements ?? throw new ArgumentNullException(nameof(staffedRequirements));
        OperationalRequirements = operationalRequirements ?? throw new ArgumentNullException(nameof(operationalRequirements));
    }

    public bool SupportsDesignation(ZoneDesignationKind designationKind)
    {
        return _supportedDesignations.Contains(designationKind);
    }

    public bool HasCapability(ZoneCapability capability)
    {
        return (Capabilities & capability) == capability;
    }
}
