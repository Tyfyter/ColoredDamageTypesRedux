using Microsoft.Xna.Framework;
using PegasusLib;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ColoredDamageTypesRedux.Calls; 
public class AddPreset : AutoModCall {
	[GetCallingMod]
	public static void Call(string name, Dictionary<string, (Color hitColor, Color critColor)> colors) => Call(CallingMod, name, colors);
	public static void Call(Mod mod, string name, Dictionary<string, (Color hitColor, Color critColor)> colors) {
		ExternalColorData externalColorData = new(mod, name, colors);
		ColoredDamageTypesRedux.loadedColorDatas.Add(externalColorData.FullName, externalColorData);
	}
}
public class AddToPreset : AutoModCall {
	public static void Call(string presetName, string damageClass, Color hitColor, Color critColor) {
		ColoredDamageTypesRedux.loadedColorDatas[presetName].ColorSet[new DamageClassDefinition(damageClass)] = new(hitColor, critColor);
	}
	public static void Call(string presetName, DamageClass damageClass, Color hitColor, Color critColor) => Call(presetName, damageClass.FullName, hitColor, critColor);
}
