using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoScannerClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoScannerComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(
        Entity<NinjaInfoScannerComponent> ent,
        ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var state = ent.Comp.VisualState switch
        {
            NinjaInfoScannerVisualState.Scan => "icon_scan",
            NinjaInfoScannerVisualState.Closed => "icon_closed",
            _ => "icon_open"
        };

        sprite.LayerSetState(0, state);
    }
}