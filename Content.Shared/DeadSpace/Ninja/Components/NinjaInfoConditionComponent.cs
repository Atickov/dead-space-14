using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaInfoConditionComponent : Component
{
    [DataField] public string TargetJobId = "";
    [DataField] public string TargetJobTitle = "";
    [DataField] public int TargetCount = 1;
    [DataField] public int CorrectScans = 0;
    [DataField] public HashSet<EntityUid> ScannedEntities = new();
}