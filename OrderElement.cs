using PegasusLib.Config;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace ColoredDamageTypesRedux {
	public class DamageClassOrderElement : OrderConfigElement<DamageClassDefinition> {
		public override UIElement GetElement(DamageClassDefinition value) => new UIText(value.DisplayName.Trim(), 0.8f) {
			Width = new(0, 1),
			Top = new(0, 0.25f),
			Height = new(FontAssets.MouseText.Value.MeasureString(value.DisplayName.Trim()).Y * 0.8f, 0),
			VAlign = 0f,
			PaddingTop = 0,
			MarginTop = 0
		};
	}
}
