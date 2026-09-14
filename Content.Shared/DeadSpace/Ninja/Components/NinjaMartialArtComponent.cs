using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaMartialArtComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = false;
}