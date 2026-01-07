using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using PegasusLib.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace ColoredDamageTypesRedux {
	public class DefaultColorData : ColorData {
		public DefaultColorData() {
			ColorSet[DamageClass.Melee] = new(new(254, 62, 2), new(253, 10, 3));
			ColorSet[DamageClass.Ranged] = new(new(33, 160, 101), new(34, 221, 101));
			ColorSet[DamageClass.Magic] = new(new(61, 142, 204), new(0, 145, 255));
			ColorSet[DamageClass.Summon] = new(new(204, 175, 61), new(255, 183, 0));
			ColorSet[DamageClass.Throwing] = new(new(33, 160, 164), new(34, 221, 209));
			interpolationMode = 0.5f;
		}
	}
	public class PillarsPreset : ColorData {
		public PillarsPreset() {
			ColorSet[DamageClass.Melee] = new(new(254, 121, 2), new(253, 62, 3));
			ColorSet[DamageClass.Ranged] = new(new(34, 221, 151), new(33, 160, 141));
			ColorSet[DamageClass.Magic] = new(new(254, 126, 229), new(255, 31, 174));
			ColorSet[DamageClass.Summon] = new(new(136, 226, 255), new(14, 154, 230));
			ColorSet[DamageClass.Throwing] = new(new(161, 114, 74), new(175, 165, 103));
			interpolated = false;
		}
	}
	public class CopyOwnColorsPreset : ColorData {
		public override bool ShowIn(OptionsID optionsID) => optionsID != OptionsID.CombatText;
		public override bool IsSpecial => true;
		public override Color? GetColor(DamageClass type, bool crit) => ColoredDamageTypesReduxConfig.SelectedColorSet.GetColor(type, crit);
	}
	public class HideDamageNumbersPreset : ColorData {
		public override bool ShowIn(OptionsID optionsID) => optionsID != OptionsID.Tooltip;
		public override bool IsSpecial => true;
		public override Color? GetColor(DamageClass type, bool crit) => Color.Transparent;
	}
}
