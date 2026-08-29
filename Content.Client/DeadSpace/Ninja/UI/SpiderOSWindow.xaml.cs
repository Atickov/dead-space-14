using System.Collections.Generic;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed partial class SpiderOSWindow : FancyWindow
{
    [Dependency] private readonly IResourceCache _resCache = default!;

    private static readonly string[] Categories = { "Ghost", "Snake", "Steel" };

    private const string TextureBasePath = "/Textures/_DeadSpace/Actions/ninja_actions.rsi/";

    private static readonly Dictionary<string, string[]> ModuleTextures = new()
    {
        ["Ghost"] = new[] { "smoke.png", "cloak.png", "clones.png", "null.png", "spiritform.png" },
        ["Snake"] = new[] { "kunai.png", "cheminjector.png", "emergencyblink.png", "caltrop.png", "null.png" },
        ["Steel"] = new[] { "shuriken.png", "adrenal.png", "emp.png", "energynet.png", "null.png" },
    };

    private readonly Dictionary<string, Dictionary<int, TextureButton>> _buttons = new();
    public event Action<int, string>? OnModuleSelected;

    public SpiderOSWindow()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        foreach (var category in Categories)
        {
            var tierButtons = new Dictionary<int, TextureButton>();

            for (var tier = 1; tier <= 5; tier++)
            {
                var buttonName = $"{category}Btn{tier}";
                var button = FindControl<TextureButton>(buttonName);

                var texturePath = new ResPath(TextureBasePath + ModuleTextures[category][tier - 1]);
                if (_resCache.TryGetResource<TextureResource>(texturePath, out var textureRes))
                {
                    button.TextureNormal = textureRes.Texture;
                }

                button.ToolTip = Loc.GetString($"spider-os-module-{category.ToLower()}-{tier}");

                var capturedTier = tier;
                var capturedCategory = category;
                button.OnPressed += _ =>
                {
                    OnModuleSelected?.Invoke(capturedTier, capturedCategory);
                };

                tierButtons[tier] = button;
            }

            _buttons[category] = tierButtons;
        }
    }

    public void UpdateState(HashSet<int> lockedTiers, Dictionary<int, string> selectedModules)
    {
        foreach (var (category, tierButtons) in _buttons)
        {
            foreach (var (tier, button) in tierButtons)
            {
                var isTierTaken = selectedModules.ContainsKey(tier);
                var isTierLocked = lockedTiers.Contains(tier);

                button.Disabled = isTierTaken || isTierLocked;
            }
        }
    }
}