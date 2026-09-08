using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

/// <summary>
/// Color scheme of the ninja gear.
/// Equipped states follow the "&lt;color&gt;-equipped-*" naming convention.
/// </summary>
public enum NinjaColorway : byte
{
    Red = 0,
    Blue = 1,
    Green = 2,
}

/// <summary>
/// Role of a cosmetic ninja item driven by the suit appearance.
/// </summary>
public enum NinjaAppearanceItemType : byte
{
    /// <summary>Only recolored by the selected colorway.</summary>
    Color = 0,

    /// <summary>Recolored and hidden/shown by the scarf toggle.</summary>
    Scarf = 1,

    /// <summary>Recolored and hidden/shown by the helmet toggle.</summary>
    Helmet = 2,
}

/// <summary>
/// Source of truth for the ninja gear appearance. Lives on the suit; the client uses the
/// networked state to repaint every equipped <see cref="NinjaAppearanceItemComponent"/> item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedNinjaAppearanceSystem))]
public sealed partial class NinjaAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaColorway Colorway = NinjaColorway.Green;

    [DataField, AutoNetworkedField]
    public bool ScarfShown = true;

    [DataField, AutoNetworkedField]
    public bool HelmetShown = true;

    [DataField]
    public EntProtoId CycleColorAction = "ActionNinjaCycleColor";

    [DataField]
    public EntProtoId ToggleScarfAction = "ActionNinjaToggleScarf";

    [DataField]
    public EntProtoId ToggleHelmetAction = "ActionNinjaToggleHelmet";

    [DataField, AutoNetworkedField]
    public EntityUid? CycleColorActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleScarfActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleHelmetActionEntity;
}

public sealed partial class CycleNinjaColorEvent : InstantActionEvent;
public sealed partial class ToggleNinjaScarfEvent : InstantActionEvent;
public sealed partial class ToggleNinjaHelmetEvent : InstantActionEvent;