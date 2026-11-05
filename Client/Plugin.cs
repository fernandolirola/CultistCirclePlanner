using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using HarmonyLib;
using UnityEngine;

namespace CultistCircle.Client;

[BepInPlugin("com.zezinho.cultistcircleplanner", "zezinho-CultistCirclePlanner", "1.0.2")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;

    private const float W = 760, H = 820, Pad = 20;

    private ConfigEntry<bool> _portuguese = null!;
    private ConfigEntry<KeyboardShortcut> _hotkey = null!;
    private ConfigEntry<int> _scale = null!;

    private readonly IconCache _icons = new();
    private readonly ClickBlocker _blocker = new();
    private readonly ToolbarButton _toolbar;
    private HideoutScreenOverlay? _overlay;
    private float _nextLookup;
    private float _lastToggle;
    private bool _open;
    private Rect _window = new(0, 0, W, H);
    private Ui _ui = null!;
    private string _tooltip = "";

    public Plugin()
    {
        _toolbar = new ToolbarButton("CultistCircleButton", CircleUnlocked);
    }

    private void Awake()
    {
        Log = Logger;
        _portuguese = Config.Bind("General", "Portuguese", false, "Show the window in Portuguese");
        _hotkey = Config.Bind("General", "Open key", new KeyboardShortcut(KeyCode.None), "Opens the Cultist Circle window in the hideout (none by default: use the circle button in the left toolbar)");
        _scale = Config.Bind("General", "Window scale %", 100, new ConfigDescription("Size of the window. Ctrl + mouse wheel over the window changes it too", new AcceptableValueRange<int>(70, 140)));
        new Harmony("com.zezinho.cultistcircleplanner").PatchAll(typeof(ScrollPatch));
    }

    private bool PT => _portuguese.Value;

    private string T(string en, string pt) => PT ? pt : en;

    private static Player? RaidPlayer => Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance is { } world && world is not HideoutGameWorld ? world.MainPlayer : null;

    private static bool InRaid => RaidPlayer != null;

    private bool InHideout => _overlay != null && _overlay.isActiveAndEnabled;

    // The circle can only be used once its hideout area is built
    private static bool CircleUnlocked() =>
        Singleton<HideoutRepresentation>.Instantiated && Singleton<HideoutRepresentation>.Instance.AreaDatas.Any(a => a.Template.Type == EAreaType.CircleOfCultists && a.CurrentLevel >= 1);

    private void Update()
    {
        // Scene-wide searches stutter on big raid maps, and the hideout screens do not exist there
        if (Time.unscaledTime >= _nextLookup)
        {
            _nextLookup = Time.unscaledTime + 1f;
            if (!InRaid && _overlay == null) _overlay = FindObjectOfType<HideoutScreenOverlay>(true);
        }

        if (_open && (!InHideout || !CircleUnlocked()))
        {
            _open = false;
        }

        if (_overlay != null)
        {
            _toolbar.Sync(_overlay, _open, () => "Cultist Circle", Toggle);
        }

        if (InHideout && _hotkey.Value.MainKey != KeyCode.None && _hotkey.Value.IsDown() && CircleUnlocked())
        {
            Toggle();
        }

        ScrollPatch.Blocked = _open && InHideout;
        _blocker.Sync(_open && InHideout, new Rect(_window.x * Ui.Scale, _window.y * Ui.Scale, _window.width * Ui.Scale, _window.height * Ui.Scale));
    }

    private void Toggle()
    {
        if (!CircleUnlocked()) return;
        if (_open)
        {
            // The game can hand over the same click twice; only that echo may not close a window it just opened
            if (Time.unscaledTime - _lastToggle >= 0.25f) _open = false;
            return;
        }

        _lastToggle = Time.unscaledTime;
        _open = true;
        _window.x = (Screen.width / Ui.Scale - _window.width) / 2;
        _window.y = (Screen.height / Ui.Scale - _window.height) / 2;
        CircleOpen();
    }

    private void OnGUI()
    {
        if (!_open || !InHideout)
        {
            return;
        }

        Ui.UserScale = _scale.Value / 100f;
        _ui ??= new Ui();
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Ui.Scale, Ui.Scale, 1));

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            _open = false;
            Event.current.Use();
        }

        // Ctrl + wheel over the window resizes it
        if (Event.current.type == EventType.ScrollWheel && Event.current.control && _window.Contains(Event.current.mousePosition))
        {
            _scale.Value = Mathf.Clamp(_scale.Value - (int)Mathf.Sign(Event.current.delta.y) * 5, 70, 140);
            Event.current.Use();
        }

        if (_open)
        {
            _window = GUI.Window(0x5A5B, _window, DrawWindow, GUIContent.none, _ui.Plain);
        }

        GUI.matrix = matrix;
    }

    private void DrawWindow(int id)
    {
        var ui = _ui;
        GUI.DrawTexture(new Rect(0, 0, W, H), ui.Bg);
        ui.Box(new Rect(0, 0, W, H), ui.Clear, ui.Line);
        GUI.DrawTexture(new Rect(0, 0, W, 3), ui.OrangeTex);
        DrawCircleWindow();
    }
}

// Read by name through reflection by the BepInEx configuration manager
internal sealed class ConfigurationManagerAttributes
{
    public bool? Browsable;
}
