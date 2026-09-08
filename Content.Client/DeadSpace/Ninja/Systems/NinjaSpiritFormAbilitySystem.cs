using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Analyzers;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaSpiritFormSystem : SharedNinjaSpiritFormSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSpiritFormComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void SetSpiritAppearance(
        Entity<NinjaSpiritFormComponent> ent,
        bool phasing)
    {
        ApplySpiritAppearance(ent.Owner, phasing ? ent.Comp.SpiritFormColor : Color.White);
    }

    private void OnHandleState(Entity<NinjaSpiritFormComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (args.State is not NinjaSpiritFormComponent.NinjaSpiritFormComponent_AutoState state)
            return;

        ApplySpiritAppearance(ent.Owner, state.SpiritFormActive ? ent.Comp.SpiritFormColor : Color.White);
    }

    private void ApplySpiritAppearance(EntityUid suit, Color color)
    {
        var xform = Transform(suit);

        if (!xform.ParentUid.IsValid())
            return;

        var user = xform.ParentUid;

        if (!TryComp<SpriteComponent>(user, out var sprite))
            return;

        _sprite.SetColor(
            (user, sprite),
            color);
    }
}