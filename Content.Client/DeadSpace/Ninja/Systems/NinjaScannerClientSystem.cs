using System.Collections.Generic;
using System.Numerics;
using Content.Shared.DeadSpace.Ninja;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaScannerClientSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, EntityUid> _disguiseEntities = new();
    private readonly Dictionary<EntityUid, bool> _originalVisibility = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NinjaSpriteEvent>(OnCopySprite);
    }

    private void OnCopySprite(NinjaSpriteEvent ev, EntitySessionEventArgs args)
    {
        var target = GetEntity(ev.Target);
        var performer = GetEntity(ev.Performer);

        if (!Exists(performer))
            return;

        if (ev.Clear)
        {
            ClearDisguise(performer);
            return;
        }

        if (!Exists(target))
            return;

        ApplyDisguise(target, performer);
    }

    private void ApplyDisguise(EntityUid target, EntityUid performer)
    {
        if (!TryComp<SpriteComponent>(target, out var targetSprite))
            return;

        if (!TryComp<SpriteComponent>(performer, out var performerSprite))
            return;

        if (!_originalVisibility.ContainsKey(performer))
            _originalVisibility[performer] = performerSprite.Visible;

        _sprite.SetVisible((performer, performerSprite), false);

        if (_disguiseEntities.Remove(performer, out var oldOverlay) && Exists(oldOverlay))
        {
            QueueDel(oldOverlay);
        }

        var overlay = EntityManager.SpawnAttachedTo(null, new EntityCoordinates(performer, Vector2.Zero));

        var overlaySprite = EnsureComp<SpriteComponent>(overlay);

        _sprite.CopySprite((target, targetSprite), (overlay, overlaySprite));

        _disguiseEntities[performer] = overlay;
    }

    private void ClearDisguise(EntityUid performer)
    {
        if (_disguiseEntities.Remove(performer, out var overlay) && Exists(overlay))
        {
            QueueDel(overlay);
        }

        if (_originalVisibility.Remove(performer, out var visible) && TryComp<SpriteComponent>(performer, out var performerSprite))
        {
            _sprite.SetVisible((performer, performerSprite), visible);
        }
    }
}