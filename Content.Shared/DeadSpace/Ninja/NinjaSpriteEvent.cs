using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[Serializable, NetSerializable]
public sealed class NinjaSpriteEvent : EntityEventArgs
{
    public NetEntity Target { get; }
    public NetEntity Performer { get; }
    public bool Clear { get; }

    public NinjaSpriteEvent(
        NetEntity target,
        NetEntity performer,
        bool clear = false)
    {
        Target = target;
        Performer = performer;
        Clear = clear;
    }
}