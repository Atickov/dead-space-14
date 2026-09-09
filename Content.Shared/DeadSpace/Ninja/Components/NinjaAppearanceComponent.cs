using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Shared.GameStates;

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
    public bool ScarfShown = false;

    [DataField, AutoNetworkedField]
    public bool HelmetShown = true;
}