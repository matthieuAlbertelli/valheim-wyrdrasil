using BepInEx.Configuration;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool.Visual;

public sealed class RegistryPlayerToolVisualConfig
{
    private const string SectionName = "Jotunkonung Ring Visual";

    private readonly ConfigEntry<string> _parentTransformName;
    private readonly ConfigEntry<float> _positionX;
    private readonly ConfigEntry<float> _positionY;
    private readonly ConfigEntry<float> _positionZ;
    private readonly ConfigEntry<float> _rotationX;
    private readonly ConfigEntry<float> _rotationY;
    private readonly ConfigEntry<float> _rotationZ;
    private readonly ConfigEntry<float> _scale;

    private RegistryPlayerToolVisualConfig(
        ConfigEntry<string> parentTransformName,
        ConfigEntry<float> positionX,
        ConfigEntry<float> positionY,
        ConfigEntry<float> positionZ,
        ConfigEntry<float> rotationX,
        ConfigEntry<float> rotationY,
        ConfigEntry<float> rotationZ,
        ConfigEntry<float> scale)
    {
        _parentTransformName = parentTransformName;
        _positionX = positionX;
        _positionY = positionY;
        _positionZ = positionZ;
        _rotationX = rotationX;
        _rotationY = rotationY;
        _rotationZ = rotationZ;
        _scale = scale;
    }

    public string ParentTransformName => _parentTransformName.Value ?? string.Empty;

    public Vector3 LocalPosition => new(_positionX.Value, _positionY.Value, _positionZ.Value);

    public Vector3 LocalEulerAngles => new(_rotationX.Value, _rotationY.Value, _rotationZ.Value);

    public Vector3 LocalScale
    {
        get
        {
            var safeScale = Mathf.Max(0.01f, _scale.Value);
            return new Vector3(safeScale, safeScale, safeScale);
        }
    }

    public static RegistryPlayerToolVisualConfig Bind(ConfigFile config)
    {
        return new RegistryPlayerToolVisualConfig(
            config.Bind(SectionName, "ParentTransformName", string.Empty, "Nom optionnel du Transform parent qui recevra le visuel de l'anneau. Laisser vide pour utiliser la résolution automatique du point d'attache du marteau."),
            config.Bind(SectionName, "LocalPositionX", 0f, "Position locale X du visuel de l'Anneau de Jötunkonung dans la main."),
            config.Bind(SectionName, "LocalPositionY", 0f, "Position locale Y du visuel de l'Anneau de Jötunkonung dans la main."),
            config.Bind(SectionName, "LocalPositionZ", 0f, "Position locale Z du visuel de l'Anneau de Jötunkonung dans la main."),
            config.Bind(SectionName, "LocalRotationX", 0f, "Rotation locale X, en degrés, du visuel de l'Anneau de Jötunkonung."),
            config.Bind(SectionName, "LocalRotationY", 0f, "Rotation locale Y, en degrés, du visuel de l'Anneau de Jötunkonung."),
            config.Bind(SectionName, "LocalRotationZ", 0f, "Rotation locale Z, en degrés, du visuel de l'Anneau de Jötunkonung."),
            config.Bind(SectionName, "UniformScale", 0.25f, "Échelle uniforme du visuel de l'Anneau de Jötunkonung."));
    }
}
