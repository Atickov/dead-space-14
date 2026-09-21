using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSuitHeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Heat = 0f;

    [DataField]
    public float MaxHeat = 100f;

    [DataField]
    public float HeatRate = 5f;

    [DataField]
    public float CoolRate = 10f;

    [DataField]
    public float EffectsThreshold = 50f;

    [DataField]
    public EntProtoId EffectPrototype = "EffectSparksBrokenEvent";

    /// <summary>
    /// Last heat value sent to clients, so the suit is not re-sent every tick. Server-side only.
    /// </summary>
    [ViewVariables]
    public float LastSentHeat = 0f;
}