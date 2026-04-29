using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod.Cil;
using Newtonsoft.Json.Linq;
using PegasusLib;
using PegasusLib.Config;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;
using static ColoredDamageTypesRedux.ColoredDamageTypesOptionsConfigElement;

namespace ColoredDamageTypesRedux {
	interface ICallOnReleaseOver {
		void LeftMouseUpOver(UIMouseEvent evt);
	}
	public class ColorsElement : UIElement, ICallOnReleaseOver {
		internal static void Patch() {
			On_Main.CursorColor += _On_Main_CursorColor;
			IL_UserInterface.Update_Inner += _IL_UserInterface_Update_Inner;
		}

		static Color? draggingColor = null;
		static void _On_Main_CursorColor(On_Main.orig_CursorColor orig) {
			orig();
			if (draggingColor.HasValue && !Main.mouseLeft && Main.mouseLeftRelease) draggingColor = null;
			if (draggingColor.HasValue) {
				Main.cursorColor = draggingColor.Value;
				if (Main.LocalPlayer is not null) Main.LocalPlayer.hasRainbowCursor = false;
			}
		}
		static void _IL_UserInterface_Update_Inner(ILContext il) {
			ILCursor c = new(il);
			FieldInfo LeftMouse = typeof(UserInterface).GetField("LeftMouse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			Type InputPointerCache = LeftMouse.FieldType;
			c.EmitLdarg2();
			c.EmitLdarg0();
			c.EmitLdfld(LeftMouse);
			c.EmitLdfld(InputPointerCache.GetField("LastDown"));
			c.EmitLdarg0();
			c.EmitLdfld(LeftMouse);
			c.EmitLdfld(InputPointerCache.GetField("WasDown"));
			c.EmitDelegate((UIElement mouseElement, UIElement lastDown, bool wasDown) => {
				if (!(Main.mouseLeft && Main.hasFocus) && wasDown && mouseElement is ICallOnReleaseOver callOnReleaseOver) {
					callOnReleaseOver.LeftMouseUpOver(new UIMouseEvent(lastDown, Main.MouseScreen));
				}
			});
		}
		public class ColorObject(PropertyFieldWrapper memberInfo, object item, ColorsElement colorsElement) {
			public Color current = (Color)memberInfo.GetValue(item);
			[LabelKey("$Config.Color.Red.Label")]
			public byte R {
				get => current.R;
				set {
					current.R = value;
					Update();
				}
			}
			[LabelKey("$Config.Color.Green.Label")]
			public byte G {
				get => current.G;
				set {
					current.G = value;
					Update();
				}
			}
			[LabelKey("$Config.Color.Blue.Label")]
			public byte B {
				get => current.B;
				set {
					current.B = value;
					Update();
				}
			}
			[LabelKey("$Config.Color.Alpha.Label")]
			public byte A {
				get => current.A;
				set {
					current.A = value;
					Update();
				}
			}
			public void Update() {
				memberInfo.SetValue(item, current);
				colorsElement.UpdateValue();
			}
		}

		public int height;

		DamageClassDefinition type;
		DamageTypeData data;
		[DisplayConfigValuesFilter<DamageClassDefinition>(typeof(ColorsElement), nameof(IsValidDamageClass)), CustomModConfigItem<DamageClassOnLeftElement>]
		public DamageClassDefinition Type {
			get => type;
			set {
				type = value;
				UpdateValue();
			}
		}
		public bool IsValidDamageClass(DamageClassDefinition newType) {
			if (newType.Equals(type)) return true;
			int id = newType.Type;
			if (!CDTRExtensions.ClassSubstituteForColor.IndexInRange(id) || CDTRExtensions.ClassSubstituteForColor[id] is not null) return false;
			UIElement parent = this;
			while (parent is not ColoredDamageTypesOptionsConfigElement && (parent = parent.Parent) is not null) ;
			if (parent is ColoredDamageTypesOptionsConfigElement source) {
				return !source.SelectedColorSet.ColorSet.ContainsKey(newType);
			}
			return false;
		}
		int startHeight;
		UIList list;
		bool readOnly;
		public bool opened = false;
		UIElement deleteButton;
		PropertyFieldWrapper GetProperty(string name) => WithAppropriateWriting(GetType().GetProperty(name));
		public PropertyFieldWrapper WithAppropriateWriting(PropertyInfo info) => new(info.WithCanWrite(!readOnly));
		public ColorsElement(DamageClassDefinition type, DamageTypeData damageTypeData, bool readOnly) {
			this.readOnly = readOnly;
			this.type = new(type.FullName);
			data = new(damageTypeData.HitColor, damageTypeData.CritColor);
			int order = 0;
			list = new() {
				Width = new(0, 1),
			};
			UIModConfigHoverImage collapseButton = new(UICommon.ButtonExpandedTexture, Language.GetTextValue("tModLoader.ModConfigCollapse"));
			collapseButton.Left.Set(readOnly ? -4f : -30f, 0f);
			collapseButton.Top.Set(5, 0f);
			collapseButton.HAlign = 1;
			collapseButton.VAlign = 0f;
			collapseButton.OnLeftClick += (_, _) => {
				opened = false;
			};
			ConfigManager.WrapIt(list, ref height, GetProperty(nameof(Type)), this, order++).Item1.Append(collapseButton);
			startHeight = height;
			height += 30;
			ColorObject c = new(new(typeof(DamageTypeData).GetProperty(nameof(DamageTypeData.HitColor))), data, this);
			ColorBox box = new(data, false);
			box.Initialize();
			list.Add(box);
			foreach (PropertyFieldWrapper variable in c.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(WithAppropriateWriting)) {
				ConfigManager.WrapIt(list, ref height, variable, c, order++);
			}
			height += 30;
			c = new(new(typeof(DamageTypeData).GetProperty(nameof(DamageTypeData.CritColor))), data, this);
			box = new(data, true);
			box.Initialize();
			list.Add(box);
			foreach (PropertyFieldWrapper variable in c.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(WithAppropriateWriting)) {
				ConfigManager.WrapIt(list, ref height, variable, c, order++);
			}
			Append(list);
			if (!readOnly) {
				deleteButton = new UIModConfigHoverImage(Main.Assets.Request<Texture2D>("Images/UI/ButtonDelete"), Language.GetTextValue("tModLoader.ModConfigRemove"));
				deleteButton.Left.Set(-4f, 0f);
				deleteButton.Top.Set(5f, 0f);
				deleteButton.HAlign = 1;
				deleteButton.VAlign = 0f;
				deleteButton.OnLeftClick += (_, _) => {
					Type = null;
				};
				Append(deleteButton);
			}
			Left.Set(0, 0);
			Width.Set(0, 1f);
			this.MaxHeight.Pixels = float.PositiveInfinity;
		}
		public override void LeftClick(UIMouseEvent evt) {
			if (evt.Target == this) opened = true;
			base.LeftClick(evt);
		}
		public event Action<DamageClassDefinition, DamageTypeData> SetValue;
		public void UpdateValue() => SetValue?.Invoke(Type, data);
		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			float targetHeight = 0;
			if (opened) {
				foreach (UIElement item in list) {
					CalculatedStyle calculatedStyle = item.GetOuterDimensions();
					targetHeight += calculatedStyle.Height;
					//targetHeight += list.ListPadding;
					//float bottom = calculatedStyle.ToRectangle().Bottom;
					//if (targetHeight < bottom) targetHeight = bottom;
				}
				targetHeight += startHeight + 60;
			} else {
				targetHeight = 32;
			}
			if (Height.Pixels != targetHeight) {
				Height.Pixels = targetHeight;
				list.Height.Pixels = targetHeight;
				list.Recalculate();
				this.Recalculate();
			}
		}
		public override void Draw(SpriteBatch spriteBatch) {
			list.IgnoresMouseInteraction = !opened;
			if (opened) {
				base.Draw(spriteBatch);
			} else {
				base.DrawSelf(spriteBatch);
				CalculatedStyle dimensions = GetDimensions();
				float settingsWidth = dimensions.Width + 1f;
				Color panelColor = IsMouseHovering ? UICommon.DefaultUIBlue : UICommon.DefaultUIBlue.MultiplyRGBA(new Color(180, 180, 180));
				ConfigElement.DrawPanel2(spriteBatch, dimensions.Position(), TextureAssets.SettingsPanel.Value, settingsWidth, dimensions.Height, panelColor);

				string text = type.DisplayName;
				Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
				CalculatedStyle innerDimensions = GetInnerDimensions();
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					FontAssets.MouseText.Value,
					text,
					innerDimensions.Position() + new Vector2(8, (innerDimensions.Height - size.Y) * 0.5f + 4),
					readOnly ? Color.Gray : Color.White,
					0f,
					Vector2.Zero,
					Vector2.One * 0.8f
				);
				GetClosedColorBoxes(out Rectangle normal, out Rectangle crit);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, normal, data.HitColor);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, crit, data.CritColor);
				deleteButton?.Draw(spriteBatch);
			}
		}
		public void GetClosedColorBoxes(out Rectangle normal, out Rectangle crit) {
			CalculatedStyle dimensions = GetDimensions();
			Rectangle box = dimensions.ToRectangle();
			box.Inflate(0, -2);
			box.Width /= 5;
			box.X = (int)(dimensions.X + dimensions.Width) - box.Width;
			if (!readOnly) box.X -= 30;
			crit = box;
			box.X -= box.Width;
			normal = box;
		}
		public override void LeftMouseDown(UIMouseEvent evt) {
			if (!opened) {
				GetClosedColorBoxes(out Rectangle normal, out Rectangle crit);
				if (normal.Contains(Main.MouseScreen)) {
					draggingColor = data.HitColor;
				} else if (crit.Contains(Main.MouseScreen)) {
					draggingColor = data.CritColor;
				}
			}
		}
		public void LeftMouseUpOver(UIMouseEvent evt) {
			if (!opened && draggingColor.HasValue) {
				GetClosedColorBoxes(out Rectangle normal, out Rectangle crit);
				if (normal.Contains(Main.MouseScreen)) {
					data.HitColor = draggingColor.Value;
				} else if (crit.Contains(Main.MouseScreen)) {
					data.CritColor = draggingColor.Value;
				}
				draggingColor = null;
			}
		}
		public class ColorBox(DamageTypeData data, bool crit) : UIElement, ICallOnReleaseOver {
			public override void OnInitialize() {
				Width.Set(0, 0.5f);
				Height.Set(30, 0f);
				HAlign = 1;
			}
			public Color Color {
				get => crit ? data.CritColor : data.HitColor;
				set {
					if (crit) {
						data.CritColor = value;
					} else {
						data.HitColor = value;
					}
				}
			}
			public override void Draw(SpriteBatch spriteBatch) => spriteBatch.Draw(TextureAssets.MagicPixel.Value, GetOuterDimensions().ToRectangle(), Color);
			public override void LeftMouseDown(UIMouseEvent evt) {
				draggingColor = Color;
			}
			public void LeftMouseUpOver(UIMouseEvent evt) {
				if (draggingColor.HasValue && draggingColor.Value != Color) Color = draggingColor.Value;
				draggingColor = null;
			}
		}
	}
	public class DamageClassOnLeftElement : NamedDefinitionConfigElement<DamageClassDefinition> {
		public override void OnBind() {
			base.OnBind();
			TextDisplayFunction = () => "";
		}
		public override void Draw(SpriteBatch spriteBatch) {
			if (opened) {
				base.Draw(spriteBatch);
			} else {
				DrawSelf(spriteBatch);
				string text = Value.DisplayName?.Trim();
				if (text is null) {
					text = Value.FullName;
				} else {
					text += $" ({Value.FullName})";
				}
				Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
				CalculatedStyle innerDimensions = GetInnerDimensions();
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					FontAssets.MouseText.Value,
					text,
					innerDimensions.Position() + new Vector2(8, (innerDimensions.Height - size.Y) * 0.5f + 4),
					Color.White,
					0f,
					Vector2.Zero,
					Vector2.One * 0.8f
				);
			}
		}
	}
}
