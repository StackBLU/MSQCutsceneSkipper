using System;
using System.Diagnostics;

using Dalamud;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MSQCutsceneSkipper
{
    public class MSQCutsceneSkipper : IDalamudPlugin
    {
        public static string Name => "MSQ Cutscene Skipper";

        // Memory values for enabling/disabling cutscene skips
        // These values patch the game's cutscene handling logic
        private const short SkipValueEnabled = -28528;
        private const short SkipValueDisabledOffset1 = 13173;
        private const short SkipValueDisabledOffset2 = 6260;

        private readonly CutsceneAddressResolver _cutsceneAddressResolver;
        private readonly object _memoryLock = new();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        // Store original values for proper restoration
        private short? _originalValue1;
        private short? _originalValue2;

        [PluginService] private ISigScanner SigScanner { get; set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; set; } = null!;

        public MSQCutsceneSkipper()
        {
            try
            {
                _cutsceneAddressResolver = new CutsceneAddressResolver();
                Initialize();
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to initialize MSQ Cutscene Skipper: {ex.Message}");
                PluginLog.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _cutsceneAddressResolver.Setup(SigScanner);

            if (!_cutsceneAddressResolver.Valid)
            {
                PluginLog.Error("Cutscene offsets not found. Plugin disabled.");
                return;
            }

            if (!StoreOriginalValues())
            {
                PluginLog.Error("Failed to read original memory values. Plugin disabled.");
                return;
            }

            if (SetCutsceneSkip(true))
            {
                _isInitialized = true;
                PluginLog.Information("MSQ Cutscene Skipper enabled");
            }
            else
            {
                PluginLog.Error("Failed to enable cutscene skipping");
                RestoreOriginalValues();
            }
        }

        private bool StoreOriginalValues()
        {
            try
            {
                lock (_memoryLock)
                {

                    if (SafeMemory.Read(_cutsceneAddressResolver.Offset1, out short value1) &&
                        SafeMemory.Read(_cutsceneAddressResolver.Offset2, out short value2))
                    {
                        _originalValue1 = value1;
                        _originalValue2 = value2;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Memory read failed: {ex.Message}");
                return false;
            }
        }

        private bool SetCutsceneSkip(bool enabled)
        {
            if (!_cutsceneAddressResolver.Valid)
            {
                return false;
            }

            try
            {
                lock (_memoryLock)
                {
                    if (_isDisposed)
                    {
                        return false;
                    }

                    var value1 = enabled ? SkipValueEnabled : SkipValueDisabledOffset1;
                    var value2 = enabled ? SkipValueEnabled : SkipValueDisabledOffset2;

                    var success1 = SafeMemory.Write(_cutsceneAddressResolver.Offset1, value1);
                    var success2 = SafeMemory.Write(_cutsceneAddressResolver.Offset2, value2);

                    return success1 && success2;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Memory write failed: {ex.Message}");
                return false;
            }
        }

        private void RestoreOriginalValues()
        {
            if (!_originalValue1.HasValue || !_originalValue2.HasValue)
            {
                return;
            }

            try
            {
                lock (_memoryLock)
                {
                    _ = SafeMemory.Write(_cutsceneAddressResolver.Offset1, _originalValue1.Value);
                    _ = SafeMemory.Write(_cutsceneAddressResolver.Offset2, _originalValue2.Value);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to restore original values: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            PluginLog.Information("Disposing MSQ Cutscene Skipper...");

            lock (_memoryLock)
            {
                _isDisposed = true;

                // Restore original memory values
                if (_isInitialized)
                {
                    RestoreOriginalValues();
                }
            }

            GC.SuppressFinalize(this);
            PluginLog.Information("MSQ Cutscene Skipper disposed");
        }
    }

    public class CutsceneAddressResolver : BaseAddressResolver
    {
        // Memory signature patterns for locating cutscene handling code
        // These patterns identify specific assembly instructions in the game binary
        private const string Offset1Pattern = "75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78";
        private const string Offset2Pattern = "74 18 8B D7 48 8D 0D";

        private readonly nint _baseAddress;

        public bool Valid => Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;
        public IntPtr Offset1 { get; private set; } = IntPtr.Zero;
        public IntPtr Offset2 { get; private set; } = IntPtr.Zero;

        public CutsceneAddressResolver()
        {
            try
            {
                _baseAddress = Process.GetCurrentProcess().MainModule?.BaseAddress ?? IntPtr.Zero;
                if (_baseAddress == IntPtr.Zero)
                {
                    MSQCutsceneSkipper.PluginLog.Error("Failed to get process base address");
                }
            }
            catch (Exception ex)
            {
                MSQCutsceneSkipper.PluginLog.Error($"Failed to initialize address resolver: {ex.Message}");
                _baseAddress = IntPtr.Zero;
            }
        }

        protected override void Setup64Bit(ISigScanner sig)
        {
            try
            {
                MSQCutsceneSkipper.PluginLog.Information("Scanning for cutscene memory signatures...");

                // Scan for memory patterns
                Offset1 = sig.ScanText(Offset1Pattern);
                Offset2 = sig.ScanText(Offset2Pattern);

                // Validate results
                if (Offset1 == IntPtr.Zero)
                {
                    MSQCutsceneSkipper.PluginLog.Error("Failed to find Offset1 pattern");
                }

                if (Offset2 == IntPtr.Zero)
                {
                    MSQCutsceneSkipper.PluginLog.Error("Failed to find Offset2 pattern");
                }

                if (Valid)
                {
                    LogOffsets();
                    MSQCutsceneSkipper.PluginLog.Information("Memory signature scan completed successfully");
                }
                else
                {
                    MSQCutsceneSkipper.PluginLog.Error("Memory signature scan failed - plugin will not function");
                }
            }
            catch (Exception ex)
            {
                MSQCutsceneSkipper.PluginLog.Error($"Exception during signature scanning: {ex.Message}");
                Offset1 = IntPtr.Zero;
                Offset2 = IntPtr.Zero;
            }
        }

        private void LogOffsets()
        {
            if (_baseAddress == IntPtr.Zero)
            {
                MSQCutsceneSkipper.PluginLog.Warning("Cannot log relative offsets - base address unknown");
                return;
            }

            try
            {
                var offset1FromBase = Offset1.ToInt64() - _baseAddress.ToInt64();
                var offset2FromBase = Offset2.ToInt64() - _baseAddress.ToInt64();

                MSQCutsceneSkipper.PluginLog.Debug($"Memory addresses found:");
                MSQCutsceneSkipper.PluginLog.Debug($"  Offset1: [\"ffxiv_dx11.exe\"+0x{offset1FromBase:X}] (0x{Offset1.ToInt64():X})");
                MSQCutsceneSkipper.PluginLog.Debug($"  Offset2: [\"ffxiv_dx11.exe\"+0x{offset2FromBase:X}] (0x{Offset2.ToInt64():X})");
            }
            catch (Exception ex)
            {
                MSQCutsceneSkipper.PluginLog.Error($"Failed to log memory offsets: {ex.Message}");
            }
        }
    }
}
