using Robust.Shared.GameStates;
using Content.Shared.Actions;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaScannerComponent : Component
{
    [DataField]
    public EntProtoId ScanAction = "ActionNinjaScan";

    [DataField, AutoNetworkedField]
    public EntityUid? ScanActionEntity;

    [DataField, AutoNetworkedField]
    public List<NinjaScanData> ScannedTargets = new();

    [DataField]
    public int MaxScans = 3;

    [DataField]
    public EntProtoId OpenUiAction = "ActionNinjaOpenScanner";

    [DataField, AutoNetworkedField]
    public EntityUid? OpenUiActionEntity;

    [DataField]
    public bool IsDisguised;

    [DataField]
    public float DisguiseEnergyCost = 1f;

    [DataField]
    public string? OriginalName;

    [DataField]
    public string? OriginalSpecies;

    [DataField]
    public MarkingSet? OriginalMarkings;

    [DataField]
    public Color? OriginalSkinColor;
}

public sealed partial class NinjaScanActionEvent : EntityTargetActionEvent;

public sealed partial class NinjaOpenScannerActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class NinjaScanData
{
    public string Name = string.Empty;
    public NetEntity Target;

    public NinjaScanData(string name, NetEntity target)
    {
        Name = name;
        Target = target;
    }
}

[Serializable, NetSerializable]
public enum NinjaScannerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NinjaScannerBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<NinjaScanData> ScannedTargets { get; }
    public bool IsDisguised { get; }

    public NinjaScannerBoundUserInterfaceState(List<NinjaScanData> scannedTargets, bool isDisguised)
    {
        ScannedTargets = scannedTargets;
        IsDisguised = isDisguised;
    }
}

[Serializable, NetSerializable]
public sealed class NinjaApplyDisguiseMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public NinjaApplyDisguiseMessage(NetEntity target)
    {
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class NinjaResetDisguiseMessage : BoundUserInterfaceMessage
{
}