using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Newtonsoft.Json;
using PegasusLib;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ColoredDamageTypesRedux {
	public class ColoredDamageTypesRedux : Mod {
		public static Dictionary<string, ColorData> loadedColorDatas = [];
		public override void Load() {
			IL_NPC.StrikeNPC_HitInfo_bool_bool += IL_NPC_StrikeNPC_HitInfo_bool_bool;
		}
		static void IL_NPC_StrikeNPC_HitInfo_bool_bool(ILContext il) {
			ILCursor c = new(il);
			int color = -1;
			c.GotoNext(
				i => i.MatchLdsfld<CombatText>(nameof(CombatText.DamagedHostileCrit)),
				i => i.MatchStloc(out color)
			);
			c.GotoNext(MoveType.After, i => i.MatchLdloc(color));
			c.EmitLdarg(1);
			c.EmitLdarg(2);
			c.EmitDelegate(static (Color _, NPC.HitInfo hit, bool fromNet) => GetColor(hit) * (fromNet ? 0.4f : 1f));
		}
		public enum Calls {
			AddPreset,
			AddToPreset
		}
		public override object Call(params object[] args) {
			switch (Enum.Parse<Calls>(args[0].ToString())) {
				case Calls.AddPreset:
				loadedColorDatas.Add((string)args[1], new ExternalColorData((Dictionary<string, (Color hitColor, Color critColor)>)args[2]));
				break;
				case Calls.AddToPreset:
				loadedColorDatas[(string)args[1]].ColorSet[new DamageClassDefinition((string)args[2])] = new((Color)args[3], (Color)args[4]);
				break;
			}
			return null;
		}
		public static Color GetColor(NPC.HitInfo hit) => ColoredDamageTypesReduxConfig.SelectedColorSet.GetFinalColor(hit.DamageType, hit.Crit)
			?? (hit.Crit ? CombatText.DamagedHostileCrit : CombatText.DamagedHostile);
	}
	public class CDTGlobalItem : GlobalItem {
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
			if (ColoredDamageTypesReduxConfig.Instance.options.applyToTooltips) {
				for (int i = 0; i < tooltips.Count; i++) {
					TooltipLine line = tooltips[i];
					if (line.FullName != "Terraria/Damage") continue;
					ColorData colorSet = ColoredDamageTypesReduxConfig.SelectedColorSet;
					if (colorSet.GetFinalColor(item.DamageType, false) is Color hitColor && colorSet.GetFinalColor(item.DamageType, true) is Color critColor) {
						line.OverrideColor = Color.Lerp(hitColor, critColor, 0.5f);
					}
				}
			}
		}
	}
	[ReinitializeDuringResizeArrays]
	public static class CDTRExtensions {
		public static DamageClass[] ClassSubstituteForColor = DamageClass.Sets.Factory.CreateNamedSet(nameof(ClassSubstituteForColor))
		.RegisterCustomSet<DamageClass>(null,
			DamageClass.MeleeNoSpeed.Type, DamageClass.Melee,
			DamageClass.SummonMeleeSpeed.Type, DamageClass.Summon
		);
		public static DamageClass DisplayDamageType(this NPC.HitInfo hit) => hit.DamageType.DisplayDamageType();
		public static DamageClass DisplayDamageType(this DamageClass damageClass) => ClassSubstituteForColor[damageClass.Type] ?? damageClass;
		public static bool UsesOwnColor(this DamageClass damageClass) => ClassSubstituteForColor[damageClass.Type] is null;
	}

	public class ColoredDamageTypesReduxConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ColoredDamageTypesReduxConfig Instance;
		public static ColorData SelectedColorSet => Instance.options.SelectedColorSet;
		public ColoredDamageTypesOptions options = new();
		public override void OnChanged() {
			SelectedColorSet.ValidatePriorityOrder();
		}
	}
	[CustomModConfigItem(typeof(ColoredDamageTypesOptionsConfigElement))]
	public class ColoredDamageTypesOptions {
		[JsonIgnore]
		internal ColorData selectedColorSet;
		[JsonIgnore]
		public ColorData SelectedColorSet {
			get {
				if (selectedColorSet is null) {
					ColoredDamageTypesRedux.loadedColorDatas.TryGetValue(selectedPreset.FullName, out selectedColorSet);
					selectedColorSet ??= CustomColors;
				}
				return selectedColorSet;
			}
		}
		[DefaultValue(true)]
		public bool applyToTooltips = true;
		public ColorDataDefinition selectedPreset = new(nameof(ColoredDamageTypesRedux), nameof(DefaultColorData));
		public CustomColorData CustomColors = new();
	}
	public abstract class ColorData : ILoadable {
		[JsonIgnore]
		public Mod Mod { get; protected set; }
		[JsonIgnore]
		public virtual string Name => GetType().Name;
		public string FullName => $"{Mod.Name}/{Name}";
		public LocalizedText DisplayName => Mod.GetLocalization("ColoredDamageTypesPreset." + Name, () => Name);
		public virtual bool ReadOnly => true;
		public bool interpolated = true;
		public DamageClassDefinition[] priorityOrder = [];
		[JsonConverter(typeof(DictionaryConverter<DamageClassDefinition, DamageTypeData>))]
		public Dictionary<DamageClassDefinition, DamageTypeData> ColorSet = [];
		public void ValidatePriorityOrder() {
			DamageClassDefinition[] priorityOrder = this.priorityOrder
			.Where(ColorSet.ContainsKey)
			.Union(ColorSet.Keys)
			.ToArray();
			if (!this.priorityOrder.SequenceEqual(priorityOrder)) this.priorityOrder = priorityOrder;
		}
		protected virtual Color? GetColor(DamageClass type, bool crit) {
			if (ColorSet.TryGetValue(new(type.Type), out DamageTypeData colors)) return crit ? colors.CritColor : colors.HitColor;
			return null;
		}
		public Color? GetFinalColor(DamageClass damageClass, bool crit) {
			damageClass = damageClass.DisplayDamageType();
			{
				if (GetColor(damageClass, crit) is Color color) return color;
			}
			if (interpolated) {
				Vector4 total = Vector4.Zero;
				Vector2 sl = Vector2.Zero;
				int count = 0;
				foreach (DamageClass type in new DamageClassList()) {
					if (damageClass.CountsAsClass(type)) {
						if (GetColor(type, crit) is Color color) {
							total += color.ToVector4();
							Vector3 hsl = Main.rgbToHsl(color);
							sl.X += hsl.Y;
							sl.Y += hsl.Z;
							count++;
						}
					}
				}
				if (count > 0) {
					Color endColor = new(total / count);
					Vector3 hsl = Main.rgbToHsl(endColor);
					hsl.Y = sl.X / count;
					hsl.Z = sl.Y / count;
					return Main.hslToRgb(hsl) with { A = endColor.A };
				}
			} else {
				DamageClassDefinition parent = priorityOrder.FirstOrDefault(d => !d.IsUnloaded && damageClass.CountsAsClass(d.DamageClass));
				if (parent is not null && GetColor(damageClass, crit) is Color color) return color;
			}
			return null;
		}
		public void Load(Mod mod) {
			Mod = mod;
			ColoredDamageTypesRedux.loadedColorDatas.Add(FullName, this);
			_ = DisplayName;
		}
		public void Unload() { }
		public class DictionaryConverter<TKey, TValue> : JsonConverter {
			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
				Dictionary<TKey, TValue> result = [];
				reader.Read();
				while (reader.TokenType == JsonToken.PropertyName) {
					TKey key = serializer.Deserialize<TKey>(reader);
					reader.Read();
					result.Add(key, serializer.Deserialize<TValue>(reader));
					reader.Read();
				}
				return result;
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
				IDictionary<TKey, TValue> values = (IDictionary<TKey, TValue>)value;
				writer.WriteStartObject();
				if (values is null) {
					writer.WriteEndObject();
					return;
				}
				foreach (KeyValuePair<TKey, TValue> item in values) {
					if (item.Key is null || item.Value is null) continue;
					StringWriter textWriter = new();
					serializer.Serialize(textWriter, item.Key);
					writer.WritePropertyName(textWriter.ToString());
					serializer.Serialize(writer, item.Value);
				}
				writer.WriteEndObject();
			}
			public override bool CanConvert(Type objectType) => objectType.IsAssignableFrom(typeof(Dictionary<TKey, TValue>));
		}
	}
	[Autoload(false)]
	public class CustomColorData : ColorData {
		public CustomColorData() {
			Mod = ModContent.GetInstance<ColoredDamageTypesRedux>();
		}

		[JsonIgnore]
		public override bool ReadOnly => false;
	}
	public class DamageTypeData(Color hitColor, Color critColor) {
		public DamageTypeData() : this(CombatText.DamagedHostile, CombatText.DamagedHostileCrit) { }
		public Color HitColor { get; set; } = hitColor;
		public Color CritColor { get; set; } = critColor;
	}
	public class ColoredDamageTypesOptionsConfigElement : ConfigElement<ColoredDamageTypesOptions> {
		protected bool needsRefresh = false;
		public override void OnBind() {
			base.OnBind();
			base.TextDisplayFunction = () => "";
			needsRefresh = true;
			normalTooltip = TooltipFunction?.Invoke() ?? string.Empty;
			TooltipFunction = () => tooltip;
			MarginRight = 8;
			Width.Set(0f, 1);
			this.MaxHeight.Pixels = float.PositiveInfinity;
			list = new() {
				Width = new(0, 1),
			};
			Append(list);
		}
		public Func<string> TextDisplayOverride { get; set; }
		int height = 0;
		string normalTooltip;
		string tooltip = string.Empty;
		bool changed = false;
		public ColoredDamageTypesOptions ChangedValue {
			get {
				if (changed.TrySet(true)) {
					JsonSerializer serializer = JsonSerializer.CreateDefault();
					StringWriter writer = new();
					serializer.Serialize(writer, Value);
					Value = serializer.Deserialize<ColoredDamageTypesOptions>(new JsonTextReader(new StringReader(writer.ToString())));
				}
				Value.selectedColorSet = null;
				needsRefresh = true;
				return Value;
			}
		}
		UIList list;
		public ColorData SelectedColorSet => Value.SelectedColorSet;
		public bool ApplyToTooltips {
			get => Value.applyToTooltips;
			set {
				if (Value.applyToTooltips != value) {
					ChangedValue.applyToTooltips = value;
				}
			}
		}
		public bool Interpolated {
			get => SelectedColorSet.interpolated;
			set {
				if (SelectedColorSet.interpolated != value) {
					ChangedValue.SelectedColorSet.interpolated = value;
				}
			}
		}
		public ColorDataDefinition SelectedPreset {
			get => Value.selectedPreset;
			set {
				if (Value.selectedPreset != value) {
					ChangedValue.selectedPreset = value;
				}
			}
		}
		[CustomModConfigItem(typeof(DamageClassOrderElement))]
		public DamageClassDefinition[] PriorityOrder {
			get => SelectedColorSet.priorityOrder;
			set {
				if (!SelectedColorSet.priorityOrder.SequenceEqual(value)) {
					ChangedValue.SelectedColorSet.priorityOrder = value;
					SelectedColorSet.ValidatePriorityOrder();
				}
			}
		}
		protected void SetupList() {
			if (ValueChanged) Value.SelectedColorSet.ValidatePriorityOrder();
			list.Clear();
			int index = 0;
			ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(ApplyToTooltips))), this, index++);
			ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(SelectedPreset))), this, index++);
			foreach (KeyValuePair<DamageClassDefinition, DamageTypeData> color in Value.SelectedColorSet.ColorSet) {
				ColorsElement colorsElement = new(color.Key, color.Value, Value.SelectedColorSet.ReadOnly);
				colorsElement.Top.Pixels += height;
				list.Add(colorsElement);
				height += colorsElement.height;
				DamageClassDefinition origKey = color.Key;
				if (!SelectedColorSet.ReadOnly) colorsElement.SetValue += (@class, colors) => {
					ChangedValue.SelectedColorSet.ColorSet.Remove(origKey);
					ChangedValue.SelectedColorSet.ColorSet.Add(@class, colors);
					SelectedColorSet.ValidatePriorityOrder();
				};
				index++;
			}
			if (!SelectedColorSet.ReadOnly) {
				UIModConfigHoverImage addButton = new(PlusTexture, Language.GetTextValue("tModLoader.ModConfigAdd"));
				addButton.Left.Set(-4f, 0f);
				addButton.HAlign = 1;
				addButton.OnLeftClick += (_, _) => {
					SoundEngine.PlaySound(in SoundID.Tink);
					DamageClassDefinition key = new();
					if (!SelectedColorSet.ColorSet.ContainsKey(key)) {
						ChangedValue.SelectedColorSet.ColorSet.Add(key, new());
					}
				};
				list.Add(addButton);
				index++;
			}
			ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(Interpolated))), this, index++);
			if (!Interpolated) {
				PropertyFieldWrapper memberInfo = new(GetType().GetProperty(nameof(PriorityOrder)));
				ConfigManager.WrapIt(list, ref height, memberInfo, this, index++);
			}
			if (SelectedColorSet.ReadOnly) {
				UIButton<LocalizedText> copyButton = new(Language.GetOrRegister($"Mods.{nameof(ColoredDamageTypesRedux)}.CopyToCustomColors"));
				copyButton.Width.Set(0, 1);
				copyButton.Height.Set(30, 0);
				copyButton.OnLeftClick += (_, _) => {
					JsonSerializer serializer = JsonSerializer.CreateDefault();
					StringWriter writer = new();
					serializer.Serialize(writer, SelectedColorSet);
					SelectedPreset = new(ColorDataDefinition.custom_colors);
					ChangedValue.CustomColors = serializer.Deserialize<CustomColorData>(new JsonTextReader(new StringReader(writer.ToString())));
				};
				list.Add(copyButton);
			}
			Recalculate();
		}

		internal class UIModConfigHoverImage(Asset<Texture2D> texture, string hoverText) : UIImage(texture) {
			protected override void DrawSelf(SpriteBatch spriteBatch) {
				base.DrawSelf(spriteBatch);
				if (base.IsMouseHovering) {
					UICommon.TooltipMouseText(hoverText);
				}
			}
		}
		public override void Update(GameTime gameTime) {
			if (needsRefresh.TrySet(false)) SetupList();
			base.Update(gameTime);
			float targetHeight = 0;
			foreach (UIElement item in list) {
				item.Update(gameTime);
				CalculatedStyle calculatedStyle = item.GetOuterDimensions();
				targetHeight += calculatedStyle.Height;
				targetHeight += list.ListPadding;
				//float bottom = calculatedStyle.ToRectangle().Bottom;
				//if (targetHeight < bottom) targetHeight = bottom;
			}
			if (Height.Pixels != targetHeight) {
				Height.Pixels = targetHeight;
				list.Height.Pixels = targetHeight;
				list.Recalculate();
				this.Recalculate();
				this.Parent.Height = this.Height;
			}
			tooltip = normalTooltip;
		}
		public override bool ContainsPoint(Vector2 point) => true;
		protected override void DrawChildren(SpriteBatch spriteBatch) {
			foreach (UIElement element in Elements) {
				element.Draw(spriteBatch);
			}
		}
	}
	public class _UIList : UIList {
		public override bool ContainsPoint(Vector2 point) => true;
	}
}
