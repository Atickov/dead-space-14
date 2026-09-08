using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

/// <summary>
/// Marks a ninja clothing item as driven by the suit's <see cref="NinjaAppearanceComponent"/>:
/// recolored on colorway change and optionally hidden/shown by the scarf or helmet toggle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedNinjaAppearanceSystem))]
public sealed partial class NinjaAppearanceItemComponent : Component
{
    [DataField]
    public NinjaAppearanceItemType ItemType = NinjaAppearanceItemType.Color;

    /// <summary>
    /// Colorway the item displays while it is not claimed by a suit's inventory. Set by the
    /// server when the item leaves the suit (dropped, handed to a non-ninja), so the color
    /// sticks to the item for the rest of the round.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NinjaColorway? FrozenColor;

    /// <summary>
    /// HideLayerClothing layers captured while the helmet was deactivated, so they can be
    /// restored verbatim when it is shown again. Server-side only.
    /// </summary>
    [ViewVariables]
    public Dictionary<HumanoidVisualLayers, SlotFlags>? SavedHideLayers;
}