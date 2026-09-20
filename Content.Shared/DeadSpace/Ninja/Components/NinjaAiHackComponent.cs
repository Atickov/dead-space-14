using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaAiHackComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(30);
}