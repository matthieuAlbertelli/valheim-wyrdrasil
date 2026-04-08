using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Services;


internal static class RegistryNpcVisualStateWriter
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
    private static readonly int HairColorPropertyId = Shader.PropertyToID("_HairColor");
    private static readonly int SkinColorPropertyId = Shader.PropertyToID("_SkinColor");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private static bool _loggedSchema;

    public static RegistryNpcVisualApplyReport Apply(
        GameObject gameObject,
        VikingIdentityData identity,
        ManualLogSource? log = null)
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var report = new RegistryNpcVisualApplyReport();
        var visEquipment = gameObject.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            return report;
        }

        LogSchemaOnce(visEquipment, log);
        ApplyAppearanceFields(visEquipment, identity.Appearance, report);
        ApplyEquipmentFields(visEquipment, identity.Equipment, report);
        return report;
    }

    public static void ApplyRuntimeRefresh(
        GameObject gameObject,
        VikingIdentityData identity,
        ManualLogSource? log = null)
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var visEquipment = gameObject.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            return;
        }

        var appearance = identity.Appearance;
        var equipment = identity.Equipment;

        SetBoolField(visEquipment, "m_isPlayer", false);

        Apply(gameObject, identity, log);
        ResetCurrentVisualCache(visEquipment);

        var hairHash = StableHashOrZero(appearance.HairItem);
        var beardHash = StableHashOrZero(appearance.BeardItem);
        var helmetHash = StableHashOrZero(equipment.HelmetItem);
        var chestHash = StableHashOrZero(equipment.ChestItem);
        var legHash = StableHashOrZero(equipment.LegItem);
        var shoulderHash = StableHashOrZero(equipment.ShoulderItem);
        var rightHash = StableHashOrZero(equipment.RightHandItem);
        var leftHash = StableHashOrZero(equipment.LeftHandItem);

        InvokeBestEffort(visEquipment, "SetModel", appearance.ModelIndex);
        InvokeBestEffort(visEquipment, "SetHairItem", appearance.HairItem);
        InvokeBestEffort(visEquipment, "SetBeardItem", appearance.BeardItem ?? string.Empty);
        InvokeBestEffort(
            visEquipment,
            "SetSkinColor",
            new Vector3(appearance.SkinColor.r, appearance.SkinColor.g, appearance.SkinColor.b));
        InvokeBestEffort(
            visEquipment,
            "SetHairColor",
            new Vector3(appearance.HairColor.r, appearance.HairColor.g, appearance.HairColor.b));
        InvokeBestEffort(visEquipment, "SetHairEquipped", hairHash);
        InvokeBestEffort(visEquipment, "SetBeardEquipped", beardHash);

        TrySetItemBySlot(visEquipment, new[] { "Helmet" }, equipment.HelmetItem, 0);
        TrySetItemBySlot(visEquipment, new[] { "Chest" }, equipment.ChestItem, 0);
        TrySetItemBySlot(visEquipment, new[] { "Leg", "Legs" }, equipment.LegItem, 0);
        TrySetItemBySlot(visEquipment, new[] { "Shoulder" }, equipment.ShoulderItem, 0);
        TrySetItemBySlot(visEquipment, new[] { "RightItem", "RightHand", "Right" }, equipment.RightHandItem, 0);
        TrySetItemBySlot(visEquipment, new[] { "LeftItem", "LeftHand", "Left" }, equipment.LeftHandItem, 0);

        InvokeBestEffort(visEquipment, "SetHelmetItem", equipment.HelmetItem ?? string.Empty);
        InvokeBestEffort(visEquipment, "SetChestItem", equipment.ChestItem ?? string.Empty);
        InvokeBestEffort(visEquipment, "SetLegItem", equipment.LegItem ?? string.Empty);
        InvokeBestEffort(visEquipment, "SetShoulderItem", equipment.ShoulderItem ?? string.Empty, 0);
        InvokeBestEffort(visEquipment, "SetRightItem", equipment.RightHandItem ?? string.Empty);
        InvokeBestEffort(visEquipment, "SetLeftItem", equipment.LeftHandItem ?? string.Empty, 0);

        InvokeBestEffort(visEquipment, "AttachArmor", chestHash, 0);
        InvokeBestEffort(visEquipment, "AttachArmor", legHash, 0);
        InvokeBestEffort(visEquipment, "AttachArmor", shoulderHash, 0);

        InvokeBestEffort(visEquipment, "SetHelmetEquipped", helmetHash, hairHash);
        InvokeBestEffort(visEquipment, "SetChestEquipped", chestHash);
        InvokeBestEffort(visEquipment, "SetLegEquipped", legHash);
        InvokeBestEffort(visEquipment, "SetShoulderEquipped", shoulderHash, 0);
        InvokeBestEffort(visEquipment, "SetRightHandEquipped", rightHash);
        InvokeBestEffort(visEquipment, "SetLeftHandEquipped", leftHash, 0);

        InvokeBestEffort(visEquipment, "UpdateBaseModel");
        InvokeBestEffort(visEquipment, "UpdateColors");
        InvokeBestEffort(visEquipment, "UpdateEquipmentVisuals");
        InvokeBestEffort(visEquipment, "UpdateVisuals");
        InvokeBestEffort(visEquipment, "UpdateLodgroup");
        InvokeBestEffort(visEquipment, "MarkDirty");

        ForceBodyModel(visEquipment, appearance.ModelIndex, log);
        TintAccessoryInstance(visEquipment, "m_hairItemInstance", appearance.HairColor);
        TintAccessoryInstance(visEquipment, "m_beardItemInstance", appearance.HairColor);
    }

    private static void ApplyAppearanceFields(
        VisEquipment visEquipment,
        VikingAppearanceData appearance,
        RegistryNpcVisualApplyReport report)
    {
        report.ModelMember = SetMemberValue(visEquipment, appearance.ModelIndex, "m_modelIndex", "m_model");
        report.HairMember = SetMemberValue(visEquipment, appearance.HairItem, "m_hairItem", "m_hair");
        report.BeardMember = SetMemberValue(visEquipment, appearance.BeardItem ?? string.Empty, "m_beardItem", "m_beard");
        report.SkinColorMember = SetColorLikeMember(visEquipment, appearance.SkinColor, "m_skinColor");
        report.HairColorMember = SetColorLikeMember(visEquipment, appearance.HairColor, "m_hairColor");
    }

    private static void ApplyEquipmentFields(
        VisEquipment visEquipment,
        VikingEquipmentData equipment,
        RegistryNpcVisualApplyReport report)
    {
        report.HelmetMember = SetMemberValue(visEquipment, equipment.HelmetItem ?? string.Empty, "m_helmetItem");
        report.ChestMember = SetMemberValue(visEquipment, equipment.ChestItem ?? string.Empty, "m_chestItem");
        report.LegMember = SetMemberValue(visEquipment, equipment.LegItem ?? string.Empty, "m_legItem", "m_legsItem");
        report.ShoulderMember = SetMemberValue(visEquipment, equipment.ShoulderItem ?? string.Empty, "m_shoulderItem");
        report.RightHandMember = SetMemberValue(visEquipment, equipment.RightHandItem ?? string.Empty, "m_rightItem");
        report.LeftHandMember = SetMemberValue(visEquipment, equipment.LeftHandItem ?? string.Empty, "m_leftItem");
    }

    private static void ForceBodyModel(VisEquipment visEquipment, int modelIndex, ManualLogSource? log)
    {
        var bodyModelField = visEquipment.GetType().GetField(
            "m_bodyModel",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var modelsField = visEquipment.GetType().GetField(
            "m_models",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var bodyRenderer = bodyModelField?.GetValue(visEquipment) as SkinnedMeshRenderer;
        var modelsArray = modelsField?.GetValue(visEquipment) as Array;

        if (bodyRenderer == null || modelsArray == null)
        {
            return;
        }

        if (modelIndex < 0 || modelIndex >= modelsArray.Length)
        {
            return;
        }

        var modelEntry = modelsArray.GetValue(modelIndex);
        if (modelEntry == null)
        {
            return;
        }

        var entryType = modelEntry.GetType();

        var meshField = entryType.GetField(
            "m_mesh",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var materialField = entryType.GetField(
            "m_baseMaterial",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var mesh = meshField?.GetValue(modelEntry) as Mesh;
        var material = materialField?.GetValue(modelEntry) as Material;

        if (mesh != null)
        {
            bodyRenderer.sharedMesh = mesh;
        }

        if (material != null)
        {
            bodyRenderer.sharedMaterial = material;
        }

        log?.LogInfo(
            $"[VisualForce] Forced body model index={modelIndex} mesh={(mesh != null ? mesh.name : "<null>")} material={(material != null ? material.name : "<null>")}");
    }

    private static void TintAccessoryInstance(VisEquipment visEquipment, string fieldName, Color color)
    {
        var field = visEquipment.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var accessoryRoot = field?.GetValue(visEquipment) as GameObject;
        if (accessoryRoot == null)
        {
            return;
        }

        var renderers = accessoryRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            var materials = renderer.materials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];

                material.color = color;

                if (material.HasProperty(ColorPropertyId))
                {
                    material.SetColor(ColorPropertyId, color);
                }

                if (material.HasProperty(EmissionColorPropertyId))
                {
                    material.SetColor(EmissionColorPropertyId, Color.black);
                }

                if (material.HasProperty(HairColorPropertyId))
                {
                    material.SetColor(HairColorPropertyId, color);
                }

                if (material.HasProperty(SkinColorPropertyId))
                {
                    material.SetColor(SkinColorPropertyId, color);
                }

                if (material.HasProperty(BaseColorPropertyId))
                {
                    material.SetColor(BaseColorPropertyId, color);
                }
            }
        }
    }

    private static void TrySetItemBySlot(VisEquipment visEquipment, string[] candidateEnumNames, string? itemName, int variant)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        var safeItemName = itemName!;

        var setItemMethod = visEquipment.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                if (method.Name != "SetItem")
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 3 && parameters[0].ParameterType.IsEnum;
            });

        if (setItemMethod == null)
        {
            return;
        }

        var slotType = setItemMethod.GetParameters()[0].ParameterType;

        foreach (var candidateName in candidateEnumNames)
        {
            try
            {
                var enumValue = Enum.Parse(slotType, candidateName, true);
                setItemMethod.Invoke(visEquipment, new[] { enumValue, (object)safeItemName, variant });
                return;
            }
            catch
            {
                // Try next candidate enum name.
            }
        }
    }

    private static void ResetCurrentVisualCache(VisEquipment visEquipment)
    {
        SetIntField(visEquipment, "m_currentBeardItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentChestItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentHairItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentHelmetItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentLeftBackItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentLeftBackItemVariant", int.MinValue);
        SetIntField(visEquipment, "m_currentLeftItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentLeftItemVariant", int.MinValue);
        SetIntField(visEquipment, "m_currentLegItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentModelIndex", int.MinValue);
        SetIntField(visEquipment, "m_currentRightBackItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentRightItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentShoulderItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentShoulderItemVariant", int.MinValue);
        SetIntField(visEquipment, "m_currentTrinketItemHash", int.MinValue);
        SetIntField(visEquipment, "m_currentUtilityItemHash", int.MinValue);
    }

    private static void SetIntField(object target, string fieldName, int value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(int))
        {
            field.SetValue(target, value);
        }
    }

    private static void SetBoolField(object target, string fieldName, bool value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(target, value);
        }
    }

    private static string? SetMemberValue(object target, object value, params string[] candidateNames)
    {
        foreach (var candidateName in candidateNames)
        {
            var field = target.GetType().GetField(
                candidateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null && field.FieldType.IsAssignableFrom(value.GetType()))
            {
                field.SetValue(target, value);
                return $"field:{candidateName}";
            }

            var property = target.GetType().GetProperty(
                candidateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property != null &&
                property.CanWrite &&
                property.PropertyType.IsAssignableFrom(value.GetType()))
            {
                property.SetValue(target, value, null);
                return $"property:{candidateName}";
            }
        }

        return null;
    }

    private static string? SetColorLikeMember(object target, Color color, params string[] candidateNames)
    {
        foreach (var candidateName in candidateNames)
        {
            var field = target.GetType().GetField(
                candidateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null && TryConvertColor(field.FieldType, color, out var convertedField))
            {
                field.SetValue(target, convertedField);
                return $"field:{candidateName}:{field.FieldType.Name}";
            }

            var property = target.GetType().GetProperty(
                candidateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property != null &&
                property.CanWrite &&
                TryConvertColor(property.PropertyType, color, out var convertedProperty))
            {
                property.SetValue(target, convertedProperty, null);
                return $"property:{candidateName}:{property.PropertyType.Name}";
            }
        }

        return null;
    }

    private static bool TryConvertColor(Type targetType, Color color, out object? converted)
    {
        if (targetType == typeof(Color))
        {
            converted = color;
            return true;
        }

        if (targetType == typeof(Vector3))
        {
            converted = new Vector3(color.r, color.g, color.b);
            return true;
        }

        if (targetType == typeof(Vector4))
        {
            converted = new Vector4(color.r, color.g, color.b, color.a);
            return true;
        }

        converted = null;
        return false;
    }

    private static void InvokeBestEffort(object target, string methodName, params object?[] args)
    {
        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == methodName)
            .ToArray();

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            var invocationArgs = new object?[args.Length];
            var match = true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var argument = args[i];

                if (argument == null)
                {
                    invocationArgs[i] = null;
                    continue;
                }

                if (parameterType.IsInstanceOfType(argument))
                {
                    invocationArgs[i] = argument;
                    continue;
                }

                try
                {
                    invocationArgs[i] = Convert.ChangeType(argument, parameterType);
                }
                catch (Exception)
                {
                    match = false;
                    break;
                }
            }

            if (!match)
            {
                continue;
            }

            try
            {
                method.Invoke(target, invocationArgs);
                return;
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }

    private static int StableHashOrZero(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? 0 : GetStableHashCode(value!);
    }

    private static int GetStableHashCode(string value)
    {
        unchecked
        {
            var hash1 = 5381;
            var hash2 = hash1;

            for (var i = 0; i < value.Length && value[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ value[i];

                if (i == value.Length - 1 || value[i + 1] == '\0')
                {
                    break;
                }

                hash2 = ((hash2 << 5) + hash2) ^ value[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    private static void LogSchemaOnce(VisEquipment visEquipment, ManualLogSource? log)
    {
        if (_loggedSchema || log == null)
        {
            return;
        }

        _loggedSchema = true;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        log.LogInfo("[VisualSchema] ===== VisEquipment full scan begin =====");

        foreach (var field in visEquipment.GetType().GetFields(flags).OrderBy(field => field.Name))
        {
            log.LogInfo($"[VisualSchema] Field {field.FieldType.Name} {field.Name}");
        }

        foreach (var property in visEquipment.GetType().GetProperties(flags).OrderBy(property => property.Name))
        {
            log.LogInfo($"[VisualSchema] Property {property.PropertyType.Name} {property.Name}");
        }

        foreach (var method in visEquipment.GetType().GetMethods(flags).OrderBy(method => method.Name))
        {
            var parameters = string.Join(
                ", ",
                method.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));

            log.LogInfo($"[VisualSchema] Method {method.ReturnType.Name} {method.Name}({parameters})");
        }

        log.LogInfo("[VisualSchema] ===== VisEquipment full scan end =====");
    }
}