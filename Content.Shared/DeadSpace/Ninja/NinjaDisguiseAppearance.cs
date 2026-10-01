// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[Serializable, NetSerializable]
public sealed class NinjaDisguiseAppearance
{
    [DataField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public ProtoId<SpeciesPrototype> Species = default!;

    [DataField]
    public Color SkinColor = Color.FromHex("#C0967F");

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public Color SpeakerColor = Color.White;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Gender Gender;

    [DataField]
    public int Age = 18;

    [DataField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField]
    public bool HairGradientEnabled;

    [DataField]
    public Color HairGradientColor = Color.Black;

    public static NinjaDisguiseAppearance Capture(HumanoidAppearanceComponent humanoid)
    {
        return new NinjaDisguiseAppearance
        {
            MarkingSet = new MarkingSet(humanoid.MarkingSet),
            Species = humanoid.Species,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            SpeakerColor = humanoid.SpeakerColor,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            Age = humanoid.Age,
            PermanentlyHidden = new HashSet<HumanoidVisualLayers>(humanoid.PermanentlyHidden),
            CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
            HairGradientEnabled = humanoid.HairGradientEnabled,
            HairGradientColor = humanoid.HairGradientColor,
        };
    }

    public void ApplyTo(HumanoidAppearanceComponent humanoid)
    {
        humanoid.MarkingSet = new MarkingSet(MarkingSet);

        if (Species.Id != null)
            humanoid.Species = Species.Id;

        humanoid.SkinColor = SkinColor;
        humanoid.EyeColor = EyeColor;
        humanoid.SpeakerColor = SpeakerColor;
        humanoid.Sex = Sex;
        humanoid.Gender = Gender;
        humanoid.Age = Age;
        humanoid.PermanentlyHidden = new HashSet<HumanoidVisualLayers>(PermanentlyHidden);
        humanoid.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(CustomBaseLayers);

        humanoid.HiddenLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>();

        humanoid.HairGradientEnabled = HairGradientEnabled;
        humanoid.HairGradientColor = HairGradientColor;
    }
}
