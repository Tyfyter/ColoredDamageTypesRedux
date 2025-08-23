using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ColoredDamageTypesRedux {
	public class ColorsElement : UIElement {
		public class ColorObject(PropertyFieldWrapper memberInfo, object item, ColorsElement colorsElement) {

			public Color current = (Color)memberInfo.GetValue(item);

			[LabelKey("$Config.Color.Red.Label")]
			public byte R {
				get {
					return current.R;
				}
				set {
					current.R = value;
					Update();
				}
			}

			[LabelKey("$Config.Color.Green.Label")]
			public byte G {
				get {
					return current.G;
				}
				set {
					current.G = value;
					Update();
				}
			}

			[LabelKey("$Config.Color.Blue.Label")]
			public byte B {
				get {
					return current.B;
				}
				set {
					current.B = value;
					Update();
				}
			}

			[LabelKey("$Config.Color.Alpha.Label")]
			public byte A {
				get {
					return current.A;
				}
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
		[ValueFilter<DamageClassDefinition>(typeof(ColorsElement), nameof(IsValidDamageClass))]
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
			while (parent is not ColoredDamageTypesOptionsConfigElement && (parent = parent.Parent) is not null);
			if (parent is ColoredDamageTypesOptionsConfigElement source) {
				return !source.SelectedColorSet.ColorSet.ContainsKey(newType);
			}
			return false;
		}
		int startHeight;
		UIList list;
		bool readOnly;
		PropertyFieldWrapper GetProperty(string name) => WithAppropriateWriting(GetType().GetProperty(name));
		public PropertyFieldWrapper WithAppropriateWriting(PropertyInfo info) {
			if (readOnly) info = new ReadOnlyPropertyInfo(info);
			return new(info);
		}
		public ColorsElement(DamageClassDefinition type, DamageTypeData damageTypeData, bool readOnly) {
			this.readOnly = readOnly;
			this.type = new(type.FullName);
			data = new(damageTypeData.HitColor, damageTypeData.CritColor);
			int order = 0;
			list = new() {
				Width = new(0, 1),
			};
			ConfigManager.WrapIt(list, ref height, GetProperty(nameof(Type)), this, order++);
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
			Left.Set(0, 0);
			Width.Set(0, 1f);
			this.MaxHeight.Pixels = float.PositiveInfinity;
		}
		public event Action<DamageClassDefinition, DamageTypeData> SetValue;
		public void UpdateValue() => SetValue?.Invoke(Type, data);
		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			float targetHeight = 0;
			foreach (UIElement item in list) {
				CalculatedStyle calculatedStyle = item.GetOuterDimensions();
				targetHeight += calculatedStyle.Height;
				//targetHeight += list.ListPadding;
				//float bottom = calculatedStyle.ToRectangle().Bottom;
				//if (targetHeight < bottom) targetHeight = bottom;
			}
			targetHeight += startHeight + 60;
			if (Height.Pixels != targetHeight) {
				Height.Pixels = targetHeight;
				list.Height.Pixels = targetHeight;
				list.Recalculate();
				this.Recalculate();
			}
		}
		public class ColorBox(DamageTypeData data, bool crit) : UIElement {
			public override void OnInitialize() {
				Width.Set(0, 0.5f);
				Height.Set(30, 0f);
				HAlign = 1;
			}
			public override void Draw(SpriteBatch spriteBatch) {
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, GetOuterDimensions().ToRectangle(), crit ? data.CritColor : data.HitColor);
			}
		}
	}
	public class ReadOnlyPropertyInfo(PropertyInfo realInfo) : PropertyInfo {
		public override bool CanWrite => false;
		public override PropertyAttributes Attributes => realInfo.Attributes;
		public override bool CanRead => realInfo.CanRead;
		public override Type PropertyType => realInfo.PropertyType;
		public override Type DeclaringType => realInfo.DeclaringType;
		public override string Name => realInfo.Name;
		public override Type ReflectedType => realInfo.ReflectedType;
		public override MethodInfo[] GetAccessors(bool nonPublic) {
			return realInfo.GetAccessors(nonPublic);
		}
		public override object[] GetCustomAttributes(bool inherit) {
			return realInfo.GetCustomAttributes(inherit);
		}
		public override object[] GetCustomAttributes(Type attributeType, bool inherit) {
			return realInfo.GetCustomAttributes(attributeType, inherit);
		}
		public override MethodInfo GetGetMethod(bool nonPublic) {
			return realInfo.GetGetMethod(nonPublic);
		}
		public override ParameterInfo[] GetIndexParameters() {
			return realInfo.GetIndexParameters();
		}
		public override MethodInfo GetSetMethod(bool nonPublic) {
			return realInfo.GetSetMethod(nonPublic);
		}
		public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) {
			return realInfo.GetValue(obj, invokeAttr, binder, index, culture);
		}
		public override bool IsDefined(Type attributeType, bool inherit) {
			return realInfo.IsDefined(attributeType, inherit);
		}
		public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) {
			realInfo.SetValue(obj, value, invokeAttr, binder, index, culture);
		}
	}
}
