using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaAiHackConditionComponent : Component
{
    [DataField]
    public bool Hacked;
    [DataField]
    public EntityUid? Mind;
}