using System;
using System.IO;
using System.Linq;
using EFT.Hideout;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CultistCircle.Client;

/// <summary>A copy of the hideout's own left toolbar button with our icon, placed after the last button that is there (other mods add theirs too).</summary>
internal sealed class ToolbarButton(string name, string resource, Func<bool> when)
{
    private static readonly AccessTools.FieldRef<HideoutScreenOverlay, ComplementaryButton> Customization = AccessTools.FieldRefAccess<HideoutScreenOverlay, ComplementaryButton>("_customizationButton");
    private static readonly AccessTools.FieldRef<HideoutScreenOverlay, ComplementaryButton> Generator = AccessTools.FieldRefAccess<HideoutScreenOverlay, ComplementaryButton>("_generatorButton");
    private static readonly AccessTools.FieldRef<ComplementaryButton, Sprite> DefaultIcon = AccessTools.FieldRefAccess<ComplementaryButton, Sprite>("_defaultIcon");
    private static readonly AccessTools.FieldRef<ComplementaryButton, Sprite> SelectedIcon = AccessTools.FieldRefAccess<ComplementaryButton, Sprite>("_selectedIcon");

    private ComplementaryButton? _button;
    private ComplementaryButton? _template;
    private bool _failed;
    private float _seen = -1;

    public bool Exists => _button != null;

    // Called every frame while the hideout overlay is around; builds the button once, then mirrors the template's visibility
    public void Sync(HideoutScreenOverlay overlay, Func<string> tooltip, Action onClick)
    {
        if (_failed)
        {
            return;
        }

        // Only once the overlay has been shown: mods like Vertical Hideout rearrange its buttons in its Awake, which runs
        // then. A moment later still, so other mods' buttons exist and ours goes under them
        if (_seen < 0 && overlay.isActiveAndEnabled) _seen = Time.unscaledTime;
        if (_seen < 0) return;
        if (_button == null && Time.unscaledTime - _seen < 2f)
        {
            return;
        }

        try
        {
            if (_button == null)
            {
                Create(overlay, tooltip, onClick);
            }

            bool visible = _template != null && _template.gameObject.activeSelf && when();
            if (_button != null && _button.gameObject.activeSelf != visible)
            {
                if (visible) Show(tooltip, onClick); else _button.gameObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Log.LogError($"Toolbar button not added: {ex}");
        }
    }

    private void Create(HideoutScreenOverlay overlay, Func<string> tooltip, Action onClick)
    {
        _template = Customization(overlay);
        var generator = Generator(overlay);
        var parent = _template.transform.parent;
        var copy = UnityEngine.Object.Instantiate(_template.gameObject, parent, false);
        copy.name = name;
        _button = copy.GetComponent<ComplementaryButton>();

        // One step past the furthest button along the row, so Vertical Hideout's bottom row works too. Buttons anchored
        // elsewhere (ones that mod leaves in place) would mix coordinate spaces, so they are skipped
        var templateRect = (RectTransform)_template.transform;
        var step = ((RectTransform)generator.transform).anchoredPosition - templateRect.anchoredPosition;
        var last = parent.GetComponentsInChildren<ComplementaryButton>(true)
            .Where(b => b != _button && b.transform.parent == parent)
            .Select(b => (RectTransform)b.transform)
            .Where(r => r.anchorMin == templateRect.anchorMin && r.anchorMax == templateRect.anchorMax && r.pivot == templateRect.pivot)
            .OrderBy(r => Vector2.Dot(r.anchoredPosition, step))
            .LastOrDefault() ?? (RectTransform)generator.transform;
        var rect = (RectTransform)copy.transform;
        if (parent.GetComponent<LayoutGroup>() == null)
        {
            rect.anchoredPosition = last.anchoredPosition + step;
        }

        copy.transform.SetSiblingIndex(last.GetSiblingIndex() + 1);

        var sprite = LoadIcon(DefaultIcon(_template));
        DefaultIcon(_button) = sprite;
        SelectedIcon(_button) = sprite;
        _button.SetIcon(sprite);
        Show(tooltip, onClick);
    }

    private void Show(Func<string> tooltip, Action onClick)
    {
        _button!.Show(_ => onClick());
        _button.SetTooltipMessages(tooltip, tooltip);
        _button.SetSelectedStatus(false);
    }

    private static Texture2D? _texture;

    private static Texture2D Texture()
    {
        if (_texture == null)
        {
            using var stream = typeof(ToolbarButton).Assembly.GetManifestResourceStream("circle.png")!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _texture.LoadImage(memory.ToArray());
        }

        return _texture;
    }

    // The button calls SetNativeSize on its icon, so the icon gets the same native size as the one it replaces
    private Sprite LoadIcon(Sprite reference)
    {
        var texture = Texture();
        float nativeWidth = reference.rect.width / reference.pixelsPerUnit;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / (nativeWidth * 0.85f));
    }
}
