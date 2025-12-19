using Dalamud;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace MSQCutsceneSkipper;

public class MSQCutsceneSkipper : IDalamudPlugin
{
    public static string Name => "MSQ Cutscene Skipper";

    private readonly CutsceneAddressResolver _resolver = new();
    private short? _orig1, _orig2;

    [PluginService]
    private ISigScanner SigScanner { get; set; } = null!;

    public MSQCutsceneSkipper()
    {
        _resolver.Setup(SigScanner);

        if (_resolver.Valid && StoreOriginals())
        {
            SetSkip(true);
        }
    }

    private bool StoreOriginals()
    {
        return SafeMemory.Read(_resolver.Offset1, out short v1) &&
               SafeMemory.Read(_resolver.Offset2, out short v2) &&
               (_orig1 = v1) != null &&
               (_orig2 = v2) != null;
    }

    private void SetSkip(bool enabled)
    {
        var skipValue = enabled ? (short) -28528 : (short) 0;
        var restoreValue1 = enabled ? skipValue : (short) 14709;
        var restoreValue2 = enabled ? skipValue : (short) 6260;

        _ = SafeMemory.Write(_resolver.Offset1, restoreValue1);
        _ = SafeMemory.Write(_resolver.Offset2, restoreValue2);
    }

    public void Dispose()
    {
        if (_orig1.HasValue && _orig2.HasValue)
        {
            _ = SafeMemory.Write(_resolver.Offset1, _orig1.Value);
            _ = SafeMemory.Write(_resolver.Offset2, _orig2.Value);
        }

        GC.SuppressFinalize(this);
    }
}

public class CutsceneAddressResolver : BaseAddressResolver
{
    public bool Valid => Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;

    public IntPtr Offset1 { get; private set; }
    public IntPtr Offset2 { get; private set; }

    protected override void Setup64Bit(ISigScanner sig)
    {
        Offset1 = sig.ScanText("75 ?? 48 8b 0d ?? ?? ?? ?? ba ?? 00 00 00 48 83 c1 10 e8 ?? ?? ?? ?? 83 78 ?? ?? 74");
        Offset2 = sig.ScanText("74 18 8B D7 48 8D 0D");
    }
}
