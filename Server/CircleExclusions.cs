using System.Text.Json;

namespace CultistCircle;

// What the optimizer never buys: items the circle rejects or that would break a sacrifice.
// Starts from the cultistcircle.com compatibility list and is saved next to the mod as circle-exclusions.json,
// where it can be edited (an item id or a base class id per entry)
public sealed class CircleExclusions
{
    public List<Entry> Categories { get; set; } = [];
    public List<Entry> Items { get; set; } = [];

    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private static string FilePath => Path.Combine(Path.GetDirectoryName(typeof(CircleExclusions).Assembly.Location) ?? ".", "circle-exclusions.json");

    public void Add(string id, string name)
    {
        if (Items.All(e => e.Id != id))
        {
            Items.Add(new Entry { Id = id, Name = name });
            Save();
        }
    }

    public void Remove(string id)
    {
        if (Items.RemoveAll(e => e.Id == id) > 0)
        {
            Save();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Read-only folder: the change lasts until the server restarts
        }
    }

    public static CircleExclusions Load()
    {
        string path = FilePath;
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<CircleExclusions>(File.ReadAllText(path)) is { } saved)
            {
                return saved;
            }
        }
        catch
        {
            // A broken file falls back to the defaults below
        }

        var defaults = new CircleExclusions
        {
            Categories = Defaults(Cats),
            Items = Defaults(Ids),
        };
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Read-only folder: the defaults still apply
        }

        return defaults;
    }

    private static List<Entry> Defaults((string id, string name)[] source) => source.Select(s => new Entry { Id = s.id, Name = s.name }).ToList();

    private static readonly (string id, string name)[] Cats =
    [
        ("543be5cb4bdc2deb348b4568", "Ammo container"),
        ("5485a8684bdc2da71d8b4567", "Ammo"),
        ("5448e54d4bdc2dcc718b4568", "Armor"),
        ("644120aa86ffbe10ee032b6f", "Armor plate"),
        ("57bef4c42459772e8d35a53b", "Armored equipment"),
        ("5448e5284bdc2dcb718b4567", "Chest rig"),
        ("543be5e94bdc2df1348b4568", "Key"),
        ("5448f3ac4bdc2dce718b4569", "Medical item"),
        ("616eb7aea207f41933308f46", "Repair kit"),
        ("5422acb9af1c889c16000029", "Weapon")
    ];

    private static readonly (string id, string name)[] Ids =
    [
        ("5d1b36a186f7742523398433", "Metal fuel tank"),
        ("5d1b371186f774253763a656", "Expeditionary fuel tank"),
        ("5449016a4bdc2d6f028b456f", "Roubles"),
        ("5696686a4bdc2da3298b456a", "Dollars"),
        ("569668774bdc2da2298b4568", "Euros"),
        ("5d235b4d86f7742e017bc88a", "GP coin"),
        ("666b11055a706400b717cfa5", "Tripwire installation kit"),
        ("62e910aaf957f2915e0a5e36", "Digital secure DSP radio transmitter"),
        ("6655e35b6bc645cb7b059912", "\"The Eye\" mortar strike signaling device"),
        ("65ddcc9cfa85b9f17d0dfb07", "Mark of The Unheard"),
        ("66d9f7256916142b3b02276e", "Radar station spare parts"),
        ("66d9f8744827a77e870ecaf1", "GARY ZONT portable electronic warfare device"),
        ("64d0b40fbe2eed70e254e2d4", "Sacred Amulet"),
        ("5ac78a9b86f7741cca0bbd8d", "Signal Jammer"),
        ("66d9f7e7099cf6adcc07a369", "KOSA UAV electronic jamming device"),
        ("67499b9b909d2013670a5029", "KOSA UAV electronic jamming device"),
        ("67499d4deca8acb2d206163b", "KOSA UAV electronic jamming device"),
        ("5ab8ebf186f7742d8b372e80", "SSO Attack 2 raid backpack (Khaki)"),
        ("61b9e1aaef9a1b5d6a79899a", "Santa's bag"),
        ("66d9f1abb16d9aacf5068468", "RSP-30 reactive signal cartridge (Special Yellow)"),
        ("675ea3d6312c0a5c4e04e317", "RSP-30 reactive signal cartridge (Firework)"),
        ("67409848d0b2f8eb9b034db9", "Sealed box"),
        ("67408903268737ef6908d432", "Contraband box"),
        ("6740987b89d5e1ddc603f4f0", "Locked case"),
        ("67449b6c89d5e1ddc603f504", "Case key"),
        ("63a898a328e385334e0640a5", "Christmas gift"),
        ("63a8970d7108f713591149f5", "Small Christmas gift"),
        ("679baa2c61f588ae2b062a24", "Key 01"),
        ("679baa4f59b8961f370dd683", "Key 02"),
        ("679baa5a59b8961f370dd685", "Key 03"),
        ("679baa9091966fe40408f149", "Key 04"),
        ("679b992329acd1f2f60985a5", "Labrys research notes"),
        ("679b946f90622daf9708da6d", "Final Moment poster"),
        ("679b94734e9ca6b3d80586a7", "Taurus poster"),
        ("679b9477708cfcb2060b9ade", "Tark Souls poster"),
        ("679b944d597ba2ed120c3d3c", "Last Breath poster"),
        ("67d3ed3271c17ff82e0a5b0b", "Key case"),
        ("6889e4cd3ff7e9cfbc0c507c", "Thumb drive with military data"),
        ("64897ffc3656831810043165", "Sealed weapon case"),
        ("64898e9db18e646e992aba47", "Sealed weapon case"),
        ("648990314b4d2b31b63a46fc", "Sealed weapon case"),
        ("648996987063b903ff4b8561", "Sealed weapon case"),
        ("6489981f7063b903ff4b8565", "Sealed weapon case"),
        ("64899a35fc093676bb0f57e3", "Sealed weapon case"),
        ("64899ec0a236de328b12db52", "Sealed weapon case"),
        ("64899f4189de40533661a0c0", "Sealed weapon case"),
        ("6489a0a4fc493c59d15fec05", "Sealed weapon case"),
        ("6489a233a236de328b12db56", "Sealed weapon case"),
        ("6489a344fc493c59d15fec0f", "Sealed weapon case"),
        ("6489a7d87063b903ff4b85cc", "Sealed weapon case"),
        ("6489a97645f9ca4ba51c4dd0", "Sealed weapon case"),
        ("6489aa584b6fc03d00374e59", "Sealed weapon case"),
        ("6489ab9645f9ca4ba51c4dd7", "Sealed weapon case"),
        ("6489b2b131a2135f0d7d0fcb", "Sealed weapon case"),
        ("6489b73cebac5a4a1b73cab7", "Sealed weapon case"),
        ("6489b88bcf0cd80b7e749069", "Sealed weapon case"),
        ("6489b91dd0c36c0a4925c4b4", "Sealed weapon case"),
        ("6489b99eebac5a4a1b73cabc", "Sealed weapon case"),
        ("6489ba157c07471efa3e03b4", "Sealed weapon case"),
        ("6489ba92a74e43447b64d5e2", "Sealed weapon case"),
        ("6489bbd6d0c36c0a4925c4b8", "Sealed weapon case"),
        ("6489bd7b79295b4f753d486a", "Sealed weapon case"),
        ("6489bed4a53c8c047c3dc361", "Sealed weapon case"),
        ("6489bfa844d98e01bc4c420e", "Sealed weapon case"),
        ("6489c03c8bc5233fdc78e788", "Sealed weapon case"),
        ("6489c0508bc5233fdc78e78c", "Sealed weapon case"),
        ("6489c0df7c07471efa3e03b8", "Sealed weapon case"),
        ("6489c0f731a2135f0d7d0fd5", "Sealed weapon case"),
        ("6489c12f8bc5233fdc78e790", "Sealed weapon case"),
        ("6489c164a53c8c047c3dc365", "Sealed weapon case"),
        ("6489c200cf0cd80b7e74906d", "Sealed weapon case"),
        ("6489c2aad0c36c0a4925c4bc", "Sealed weapon case"),
        ("6489c30331a2135f0d7d0fd9", "Sealed weapon case"),
        ("6489c46eebac5a4a1b73cac0", "Sealed weapon case"),
        ("6489c5cb44d98e01bc4c4212", "Sealed weapon case"),
        ("6489c65cd0c36c0a4925c4c0", "Sealed weapon case"),
        ("6489c753a74e43447b64d5e6", "Sealed weapon case"),
        ("6489c80e7c07471efa3e03bc", "Sealed weapon case"),
        ("6489c8a1a53c8c047c3dc369", "Sealed weapon case"),
        ("6489c8dba53c8c047c3dc36d", "Sealed weapon case"),
        ("6489c99f7c07471efa3e03c0", "Sealed weapon case"),
        ("6489ca66d0c36c0a4925c4c4", "Sealed weapon case"),
        ("6489cad98bc5233fdc78e794", "Sealed weapon case"),
        ("6489cb4fa74e43447b64d5ea", "Sealed weapon case"),
        ("6489cc4379295b4f753d486e", "Sealed weapon case"),
        ("6489cca131a2135f0d7d0fdd", "Sealed weapon case"),
        ("6489d812cf0cd80b7e749071", "Sealed weapon case"),
        ("6489d89debac5a4a1b73caf7", "Sealed weapon case"),
        ("61605e13ffa6e502ac5e7eef", "Vortex Ranger 1500 rangefinder"),
        ("6034d103ca006d2dca39b3f0", "Hazard 4 Takedown sling backpack (Black)"),
        ("6038d614d10cbf667352dd44", "Hazard 4 Takedown sling backpack (MultiCam)"),
        ("5c0e774286f77468413cc5b2", "Mystery Ranch Blackjack 50 backpack (MultiCam)"),
        ("59e763f286f7742ee57895da", "Pilgrim tourist backpack"),
        ("5df8a4d786f77412672a1e3b", "6Sh118 raid backpack (EMR)"),
        ("656e0436d44a1bb4220303a0", "Mystery Ranch SATL Bridger Assault Pack (Foliage)"),
        ("5f5e46b96bdad616ad46d613", "Eberlestock F4 Terminator load bearing backpack (Tiger Stripe)"),
        ("6034d2d697633951dc245ea6", "Eberlestock G2 Gunslinger II backpack (Dry Earth)"),
        ("67458730df3c1da90b0b052b", "5.11 Tactical RUSH 100 backpack (Black)"),
        ("66d98233302686954b0c6f81", "RSP-30 reactive signal cartridge (Blue)")
    ];
}
