using Dalamud;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics;

namespace MSQCutsceneSkipper
{
	public class MSQCutsceneSkipper : IDalamudPlugin
	{
		private const short SkipValueEnabled = -28528;
		private const short SkipValueDisabledOffset1 = 13173;
		private const short SkipValueDisabledOffset2 = 6260;
		private readonly CutsceneAddressResolver cutsceneAddressResolver;

		[PluginService] private ISigScanner SigScanner { get; set; }
		[PluginService] public static IPluginLog PluginLog { get; set; }

		public MSQCutsceneSkipper()
		{
			cutsceneAddressResolver = new CutsceneAddressResolver();
			cutsceneAddressResolver.Setup(SigScanner);

			if (!cutsceneAddressResolver.Valid)
			{
				PluginLog.Error("Cutscene offset not found.");
				PluginLog.Warning("Plugin disabling...");
				Dispose();
				return;
			}

			PluginLog.Information("Cutscene offsets found");
			SetCutsceneSkip(true);
		}

		private void SetCutsceneSkip(bool enabled)
		{
			_ = SafeMemory.Write(cutsceneAddressResolver.Offset1, enabled ? SkipValueEnabled : SkipValueDisabledOffset1);
			_ = SafeMemory.Write(cutsceneAddressResolver.Offset2, enabled ? SkipValueEnabled : SkipValueDisabledOffset2);
		}

		public void Dispose()
		{
			SetCutsceneSkip(false);
			GC.SuppressFinalize(this);
		}
	}

	public class CutsceneAddressResolver : BaseAddressResolver
	{
		private const string Offset1Pattern = "75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78";
		private const string Offset2Pattern = "74 18 8B D7 48 8D 0D";
		private readonly nint _baseAddress = Process.GetCurrentProcess().MainModule!.BaseAddress;

		public bool Valid
		{
			get
			{
				return Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;
			}
		}

		public IntPtr Offset1 { get; private set; }
		public IntPtr Offset2 { get; private set; }

		protected override void Setup64Bit(ISigScanner sig)
		{
			Offset1 = sig.ScanText(Offset1Pattern);
			Offset2 = sig.ScanText(Offset2Pattern);
			LogOffsets();
		}

		private void LogOffsets()
		{
			long offset1FromBase = Offset1 - _baseAddress;
			long offset2FromBase = Offset2 - _baseAddress;

			MSQCutsceneSkipper.PluginLog.Information($"Offset1: [\"ffxiv_dx11.exe\"+{offset1FromBase:X}]");
			MSQCutsceneSkipper.PluginLog.Information($"Offset2: [\"ffxiv_dx11.exe\"+{offset2FromBase:X}]");
		}
	}
}