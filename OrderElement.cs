using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;
using static System.Net.Mime.MediaTypeNames;

namespace ColoredDamageTypesRedux {
	public abstract class OrderElement<T> : ConfigElement<T[]> {
		public abstract UIElement GetElement(T value);
		public virtual string GetTooltip(T value) => null;
		string normalTooltip;
		string tooltip = string.Empty;
		public int held = -1;
		public override void OnBind() {
			base.OnBind();
			normalTooltip = TooltipFunction?.Invoke() ?? string.Empty;
			TooltipFunction = () => tooltip;
			Setup();
		}
		public void Setup() {
			int height = 30;
			T[] values = Value;
			for (int i = 0; i < values.Length; i++) {
				int index = i;
				T option = values[i];
				UIElement element = GetElement(option);
				element.Top.Pixels += 4;
				ItemPanel panel = new(index) {
					Left = new(0, 0),
					Top = new(height + 2, 0),
					Width = new(-8, 1),
					Height = new(element.Height.Pixels + 4, 0),
					HAlign = 0.5f,
					PaddingTop = 0
				};
				panel.OnUpdate += element => {
					if (element is not UIPanel panel) return;
					if (panel.IsMouseHovering && held == -1) {
						panel.BackgroundColor = UICommon.DefaultUIBlue;
						tooltip = GetTooltip(option) ?? normalTooltip;
					} else {
						panel.BackgroundColor = UICommon.MainPanelBackground;
					}
				};
				panel.OnUpdate += (_) => {
					panel.Left.Pixels = 12 * (held == index).ToInt();
				};
				panel.Append(element);
				Append(panel);
				height += (int)panel.Height.Pixels + 4;
			}
			Height.Pixels = height + 4;
		}
		public override void LeftClick(UIMouseEvent evt) {
			if (held != -1) {
				int countBefore;
				List<T> values = [];
				for (countBefore = 0; countBefore < Elements.Count; countBefore++) {
					if (evt.MousePosition.Y < Elements[countBefore].GetDimensions().Center().Y) {
						break;
					}
				}
				for (int i = 0; i < Value.Length; i++) {
					if (i == countBefore) values.Add(Value[held]);
					if (i != held) values.Add(Value[i]);
				}
				if (!values.SequenceEqual(Value)) Value = values.ToArray();
				held = -1;
			} else if (evt.Target.Parent is ItemPanel itemPanel) {
				held = itemPanel.Index;
			}
		}
		public override void Update(GameTime gameTime) {
			tooltip = normalTooltip;
			base.Update(gameTime);
		}
		protected override void DrawChildren(SpriteBatch spriteBatch) {
			if (IsMouseHovering && held != -1) {
				int countBefore;
				for (countBefore = 0; countBefore < Elements.Count; countBefore++) {
					float elementerY = Elements[countBefore].GetDimensions().Center().Y;
					if (Main.mouseY < elementerY) {
						break;
					}
				}
				int height = Elements[^1].GetOuterDimensions().ToRectangle().Bottom + 2;
				if (countBefore < Elements.Count) {
					height = Elements[countBefore].GetOuterDimensions().ToRectangle().Top;
				}
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, GetInnerDimensions().ToRectangle() with { Y = height - 2, Height = 2 }, Color.White * 0.8f);
			}
			base.DrawChildren(spriteBatch);
		}
		public class ItemPanel(int index) : UIPanel {
			public int Index => index;
		}
	}
	public class DamageClassOrderElement : OrderElement<DamageClassDefinition> {
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
