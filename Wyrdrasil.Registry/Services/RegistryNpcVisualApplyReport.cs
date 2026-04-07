namespace Wyrdrasil.Registry.Services;

internal sealed class RegistryNpcVisualApplyReport
{
    public string? ModelMember { get; set; }
    public string? HairMember { get; set; }
    public string? BeardMember { get; set; }
    public string? SkinColorMember { get; set; }
    public string? HairColorMember { get; set; }
    public string? HelmetMember { get; set; }
    public string? ChestMember { get; set; }
    public string? LegMember { get; set; }
    public string? ShoulderMember { get; set; }
    public string? RightHandMember { get; set; }
    public string? LeftHandMember { get; set; }

    public string ToSummary()
    {
        return $"model={ModelMember ?? "<none>"}, hair={HairMember ?? "<none>"}, beard={BeardMember ?? "<none>"}, skin={SkinColorMember ?? "<none>"}, hairColor={HairColorMember ?? "<none>"}, helmet={HelmetMember ?? "<none>"}, chest={ChestMember ?? "<none>"}, legs={LegMember ?? "<none>"}, shoulder={ShoulderMember ?? "<none>"}, right={RightHandMember ?? "<none>"}, left={LeftHandMember ?? "<none>"}";
    }
}
