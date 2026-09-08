using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Actions;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class NinjaEmergencyTeleportSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turfSystem = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedNinjaSmokeAbilitySystem _smoke = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, NinjaEmergencyTeleportEvent>(OnTeleport);
        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, GetItemActionsEvent>(OnGetActions);
    }

    private void OnMapInit(Entity<NinjaEmergencyTeleportComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.TeleportActionEntity, comp.TeleportAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaEmergencyTeleportComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;
        args.AddAction(ent.Comp.TeleportActionEntity);
    }

    private void OnTeleport(Entity<NinjaEmergencyTeleportComponent> ent, ref NinjaEmergencyTeleportEvent args)
    {
        if (TryComp<PullableComponent>(args.Performer, out var pull) && _pulling.IsPulled(args.Performer, pull))
            _pulling.TryStopPull(args.Performer, pull);

        if (TryComp<PullerComponent>(args.Performer, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable);

        _audio.PlayPredicted(ent.Comp.TeleportSound, ent, args.Performer);

        if (_net.IsClient)
            return;

        var targetCoords = SelectRandomTileInFacingArea(args.Performer, ent.Comp.TeleportRadius);

        if (targetCoords == null)
            return;

        if (!_ninja.TryUseCharge(args.Performer, ent.Comp.EnergyCost))
            return;

        if (TryComp<NinjaSmokeAbilityComponent>(ent, out var smokeComp))
            _smoke.TrySpawnNinjaSmoke((ent.Owner, smokeComp), true);

        _transform.SetCoordinates(args.Performer, targetCoords.Value);
        args.Handled = true;
    }
    private EntityCoordinates? SelectRandomTileInFacingArea(EntityUid uid, Vector2 radius, int tries = 80, PhysicsComponent? physicsComponent = null)
    {
        var userXform = Transform(uid);
        var userCoords = userXform.Coordinates;

        if (!Resolve(uid, ref physicsComponent))
            return null;

        var forward = userXform.LocalRotation.ToWorldVec().Normalized();
        var side = new Vector2(-forward.Y, forward.X);
        var minDistance = MathF.Max(1f, radius.X);
        var maxDistance = MathF.Max(minDistance, radius.Y);
        var collisionMask = (CollisionGroup)physicsComponent.CollisionMask;

        return TryPickTile(minDistance, maxDistance)
               ?? (minDistance > 1f ? TryPickTile(1f, minDistance) : null);

        EntityCoordinates? TryPickTile(float min, float max)
        {
            for (var i = 0; i < tries; i++)
            {
                var distance = (max - min) * MathF.Sqrt(_random.NextFloat()) + min;

                var lateralOffset = _random.NextFloat(-distance / 2f, distance / 2f);
                var candidateCoords = userCoords.Offset(forward * distance + side * lateralOffset);

                if (!_turfSystem.TryGetTileRef(candidateCoords, out var tileRef)
                    || tileRef.Value.Tile.IsEmpty
                    || _turfSystem.IsTileBlocked(tileRef.Value, collisionMask))
                    continue;

                return _turfSystem.GetTileCenter(tileRef.Value);
            }

            return null;
        }
    }
}
