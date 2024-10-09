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
		[PluginService] public static IDalamudPluginInterface Interface { get; private set; }
		[PluginService] public static ISigScanner SigScanner { get; private set; }
		[PluginService] public static ICommandManager CommandManager { get; private set; }
		[PluginService] public static IChatGui ChatGui { get; private set; }
		[PluginService] public static IPluginLog PluginLog { get; private set; }

		public CutsceneAddressResolver Address { get; }

		public MSQCutsceneSkipper()
		{
			Address = new CutsceneAddressResolver();
			Address.Setup(SigScanner);

			if (Address.Valid)
			{
				PluginLog.Information("Cutscene offset found");
				SetEnabled(true);
			}
			else
			{
				PluginLog.Error("Cutscene offset not found");
				PluginLog.Warning("Plugin disabling...");
				Dispose();
				return;
			}
		}

		public void Dispose()
		{
			SetEnabled(false);
			GC.SuppressFinalize(this);
		}



		public void SetEnabled(bool isEnable)
		{
			if (!Address.Valid)
			{
				return;
			}

			if (isEnable)
			{
				_ = SafeMemory.Write<short>(Address.Offset1, -28528);
				_ = SafeMemory.Write<short>(Address.Offset2, -28528);
			}
			else
			{
				_ = SafeMemory.Write<short>(Address.Offset1, 13173);
				_ = SafeMemory.Write<short>(Address.Offset2, 6260);
			}
		}
	}

	public class CutsceneAddressResolver : BaseAddressResolver
	{
		public bool Valid
		{
			get
			{
				return Offset1 != nint.Zero && Offset2 != nint.Zero;
			}
		}

		public nint Offset1 { get; private set; }
		public nint Offset2 { get; private set; }

		protected override void Setup64Bit(ISigScanner sig)
		{
			Offset1 = sig.ScanText("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
			Offset2 = sig.ScanText("74 18 8B D7 48 8D 0D");

			MSQCutsceneSkipper.PluginLog.Information(
				"Offset1: [\"ffxiv_dx11.exe\"+{0}]",
				(Offset1.ToInt64() - Process.GetCurrentProcess().MainModule!.BaseAddress.ToInt64()).ToString("X")
				);

			MSQCutsceneSkipper.PluginLog.Information(
				"Offset2: [\"ffxiv_dx11.exe\"+{0}]",
				(Offset2.ToInt64() - Process.GetCurrentProcess().MainModule!.BaseAddress.ToInt64()).ToString("X")
				);
		}
	}
}