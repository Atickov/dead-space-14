using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaInfoScannerComponent : Component
{
    [DataField]
    public string ContainerId = "entity_storage";

    [DataField, AutoNetworkedField]
    public bool IsScanning;

    [DataField, AutoNetworkedField]
    public EntityUid? ScanningEntity;

    [DataField, AutoNetworkedField]
    public float ScanTime = 10f;

    [DataField, AutoNetworkedField]
    public NinjaInfoScannerVisualState VisualState = NinjaInfoScannerVisualState.Open;

    [DataField]
    public string ScanReagent = "Nocturine";

    [DataField]
    public float ScanReagentAmount = 10f;
}

public enum NinjaInfoScannerVisualState : byte
{
    Open,
    Closed,
    Scan
}