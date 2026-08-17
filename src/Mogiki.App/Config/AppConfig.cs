using Avalonia.Input;

namespace Mogiki.App.Config;

public sealed class KeyBindings
{
    public Key Up { get; set; } = Key.Up;
    public Key Down { get; set; } = Key.Down;
    public Key Left { get; set; } = Key.Left;
    public Key Right { get; set; } = Key.Right;
    public Key A { get; set; } = Key.X;
    public Key B { get; set; } = Key.Z;
    public Key Start { get; set; } = Key.S;
    public Key Select { get; set; } = Key.A;
}

public enum AspectRatioMode
{
    Standard4_3,    // 4:3 Standard TV (Default)
    Native8_7,      // 8:7 NTSC PAR (Authentic ~274x240)
    PixelPerfect1_1 // 1:1 Pixel Perfect (256x240)
}

public sealed class AppConfig
{
    public KeyBindings Keys { get; set; } = new();
    public int WindowScale { get; set; } = 3;
    public string LastRomPath { get; set; } = "";
    public string LibraryDirectory { get; set; } = "";
    public List<string> RecentRoms { get; set; } = [];
    public AspectRatioMode AspectRatio { get; set; } = AspectRatioMode.Standard4_3;
    public bool BilinearFilter { get; set; } = false;
    public int Volume { get; set; } = 100; // 0 to 100
    public bool SoundEnabled { get; set; } = true;

    public void AddRecentRom(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        RecentRoms.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
        RecentRoms.Insert(0, path);

        if (RecentRoms.Count > 10)
            RecentRoms.RemoveRange(10, RecentRoms.Count - 10);
    }

    public void Load(string filename = "config.ini")
    {
        if (!File.Exists(filename)) return;

        RecentRoms.Clear();
        foreach (var line in File.ReadAllLines(filename))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('['))
                continue;

            int eq = trimmed.IndexOf('=');
            if (eq < 0) continue;

            string key = trimmed[..eq].Trim().ToLowerInvariant();
            string val = trimmed[(eq + 1)..].Trim();

            switch (key)
            {
                case "up" when Enum.TryParse<Key>(val, out var k): Keys.Up = k; break;
                case "down" when Enum.TryParse<Key>(val, out var k): Keys.Down = k; break;
                case "left" when Enum.TryParse<Key>(val, out var k): Keys.Left = k; break;
                case "right" when Enum.TryParse<Key>(val, out var k): Keys.Right = k; break;
                case "a" when Enum.TryParse<Key>(val, out var k): Keys.A = k; break;
                case "b" when Enum.TryParse<Key>(val, out var k): Keys.B = k; break;
                case "start" when Enum.TryParse<Key>(val, out var k): Keys.Start = k; break;
                case "select" when Enum.TryParse<Key>(val, out var k): Keys.Select = k; break;

                case "scale" when int.TryParse(val, out int scale):
                    WindowScale = Math.Clamp(scale, 1, 6);
                    break;

                case "volume" when int.TryParse(val, out int vol):
                    Volume = Math.Clamp(vol, 0, 100);
                    break;

                case "sound" when bool.TryParse(val, out bool sound):
                    SoundEnabled = sound;
                    break;

                case "aspect" when Enum.TryParse<AspectRatioMode>(val, out var aspect):
                    AspectRatio = aspect;
                    break;

                case "bilinear" when bool.TryParse(val, out bool filter):
                    BilinearFilter = filter;
                    break;

                case "lastrom":
                    LastRomPath = val;
                    break;

                case "library":
                    LibraryDirectory = val;
                    break;

                case "recent":
                    if (File.Exists(val) && !RecentRoms.Contains(val))
                        RecentRoms.Add(val);
                    break;
            }
        }
    }

    public void Save(string filename = "config.ini")
    {
        using var writer = new StreamWriter(filename);
        writer.WriteLine("# Mogiki NES Emulator Configuration");
        writer.WriteLine("[Controls]");
        writer.WriteLine($"up={Keys.Up}");
        writer.WriteLine($"down={Keys.Down}");
        writer.WriteLine($"left={Keys.Left}");
        writer.WriteLine($"right={Keys.Right}");
        writer.WriteLine($"a={Keys.A}");
        writer.WriteLine($"b={Keys.B}");
        writer.WriteLine($"start={Keys.Start}");
        writer.WriteLine($"select={Keys.Select}");
        writer.WriteLine();
        writer.WriteLine("[Display]");
        writer.WriteLine($"scale={WindowScale}");
        writer.WriteLine($"aspect={AspectRatio}");
        writer.WriteLine($"bilinear={BilinearFilter}");
        writer.WriteLine();
        writer.WriteLine("[Audio]");
        writer.WriteLine($"sound={SoundEnabled}");
        writer.WriteLine($"volume={Volume}");
        writer.WriteLine();
        writer.WriteLine("[Recent]");
        writer.WriteLine($"lastrom={LastRomPath}");
        writer.WriteLine($"library={LibraryDirectory}");
        foreach (var recent in RecentRoms)
        {
            writer.WriteLine($"recent={recent}");
        }
    }
}
