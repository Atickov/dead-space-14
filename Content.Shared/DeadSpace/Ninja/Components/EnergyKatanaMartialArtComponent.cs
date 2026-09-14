using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EnergyKatanaMartialArtComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ReflectProbBonus = 0.1f;

    [DataField]
    public float OriginalReflectProb;

    [DataField]
    public bool Boosted = false;
}