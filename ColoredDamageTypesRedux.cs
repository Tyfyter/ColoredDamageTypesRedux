using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Newtonsoft.Json;
using PegasusLib;
using PegasusLib.Config;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
using tModPorter;

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
			c.EmitDelegate(static (Color _, NPC.HitInfo hit, bool fromNet) => GetColor(hit, fromNet));
		}
		public enum Calls {
			AddPreset,
			AddToPreset
		}
		public override object Call(params object[] args) {
			switch (Enum.Parse<Calls>(args[0].ToString())) {
				case Calls.AddPreset:
				ExternalColorData externalColorData = new((Mod)args[1], (string)args[2], (Dictionary<string, (Color hitColor, Color critColor)>)args[3]);
				loadedColorDatas.Add(externalColorData.FullName, externalColorData);
				break;
				case Calls.AddToPreset:
				loadedColorDatas[(string)args[1]].ColorSet[new DamageClassDefinition((string)args[2])] = new((Color)args[3], (Color)args[4]);
				break;
			}
			return null;
		}
		public static Color GetColor(NPC.HitInfo hit, bool fromNet) {
			Color color = (fromNet ? OtherPlayersColorsConfig.SelectedColorSet : ColoredDamageTypesReduxConfig.SelectedColorSet).GetFinalColor(hit.DamageType, hit.Crit)
			?? (hit.Crit ? CombatText.DamagedHostileCrit : CombatText.DamagedHostile);
			if (fromNet && OtherPlayersColorsConfig.SelectedColorSet is CopyOwnColorsPreset) color *= 0.4f;
			return color;
		}
		// for DevHelper
		static string DevHelpBrokenReason {
			get {
#if DEBUG
				return "Mod was last built in DEBUG configuration";
#else
				return null;
#endif
			}
		}
	}
	public class CDTGlobalItem : GlobalItem {
		static readonly Regex csoRegex = new("^([\\d+]+) (?:\\(\\[c\\/([\\da-f]{6}):[\\d+]+\\]\\))?", RegexOptions.Compiled);
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
			if (ColoredDamageTypesReduxConfig.Instance.options.applyToTooltips) {
				for (int i = 0; i < tooltips.Count; i++) {
					TooltipLine line = tooltips[i];
					if (line.FullName != "Terraria/Damage") continue;
					ColorData colorSet = ColoredDamageTypesReduxConfig.SelectedColorSet;
					if (colorSet.GetFinalColor(item.DamageType, false) is Color hitColor && colorSet.GetFinalColor(item.DamageType, true) is Color critColor) {
						line.OverrideColor = Color.Lerp(hitColor, critColor, 0.5f);
						if (ColoredDamageTypesReduxConfig.Instance.options.CSOCompatActive) {
							line.Text = csoRegex.Replace(line.Text, match => {
								string value = match.Value;
								Group critText = match.Groups[2];
								if (critText.Success) value = value.Replace(critText.Value, critColor.Hex3());
								value = $"[c/{hitColor.Hex3()}:{match.Groups[1].Value}]" + value[match.Groups[1].Length..];
								return value;
							});
						}
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
		[DefaultValue(typeof(ColoredDamageTypesOptions), "false")]
		public ColoredDamageTypesOptions options = new();
		public override void OnLoaded() {
			if (options is null) {
				options = new();
				SaveChanges(this);
			}
		}
		public override void OnChanged() {
			if (ColoredDamageTypesRedux.loadedColorDatas.Count > 0) SelectedColorSet.ValidatePriorityOrder();
		}
		/*static ColoredDamageTypesReduxConfig() {
			Directory.CreateDirectory(ConfigManager.ModConfigPath);
			string filename = nameof(ColoredDamageTypesRedux) + "_" + nameof(ColoredDamageTypesReduxConfig) + ".json";
			string path = Path.Combine(ConfigManager.ModConfigPath, filename);
			if (File.Exists(path)) return;
			string json = "{\"options\":{\"selectedPreset\": \"ColoredDamageTypesRedux/DefaultColorData\"}}";
			File.WriteAllText(path, json);
		}*/
	}

	public class OtherPlayersColorsConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static OtherPlayersColorsConfig Instance;
		public static ColorData SelectedColorSet => Instance.options.SelectedColorSet;
		[DefaultValue(typeof(ColoredDamageTypesOptions), "true")]
		public ColoredDamageTypesOptions options = new(true);
		public override void OnLoaded() {
			if (options is null) {
				options = new(true);
				SaveChanges(this);
			}
		}
		public override void OnChanged() {
			options.isRemoteColors = true;
			if (ColoredDamageTypesRedux.loadedColorDatas.Count > 0) SelectedColorSet.ValidatePriorityOrder();
		}
		/*static OtherPlayersColorsConfig() {
			Directory.CreateDirectory(ConfigManager.ModConfigPath);
			string filename = nameof(ColoredDamageTypesRedux) + "_" + nameof(OtherPlayersColorsConfig) + ".json";
			string path = Path.Combine(ConfigManager.ModConfigPath, filename);
			if (File.Exists(path)) return;
			string json = "{\"options\":{\"isRemoteColors\": true, \"selectedPreset\": \"ColoredDamageTypesRedux/CopyOwnColorsPreset\"}}";
			File.WriteAllText(path, json);
		}*/
	}
	[CustomModConfigItem(typeof(ColoredDamageTypesOptionsConfigElement))]
	public class ColoredDamageTypesOptions(bool isRemoteColors = false) {
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
		public bool isRemoteColors = isRemoteColors;
		[JsonIgnore]
		static bool? _csoEnabled;
		[JsonIgnore]
		public static bool CSOEnabled => _csoEnabled ??= ModLoader.HasMod("CritRework");
		[JsonIgnore]
		public bool CSOCompatActive => csoCompat && CSOEnabled;

		[DefaultValue(true)]
		public bool applyToTooltips = true;
		[DefaultValue(0.5f)]
		public float tooltipCritness = 0.5f;
		[DefaultValue(true)]
		public bool csoCompat = true;
		public ColorDataDefinition selectedPreset = new(nameof(ColoredDamageTypesRedux), isRemoteColors ? nameof(CopyOwnColorsPreset) : nameof(DefaultColorData));
		public CustomColorData CustomColors = new();
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
					bool isRemoteColors = Value.isRemoteColors;
					JsonSerializer serializer = JsonSerializer.CreateDefault();
					StringWriter writer = new();
					serializer.Serialize(writer, Value);
					Value = serializer.Deserialize<ColoredDamageTypesOptions>(new JsonTextReader(new StringReader(writer.ToString())));
					Value.isRemoteColors = isRemoteColors;
				}
				Value.selectedColorSet = null;
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
					needsRefresh = true;
				}
			}
		}
		public float TooltipCritness {
			get => Value.tooltipCritness;
			set {
				if (Value.tooltipCritness != value) {
					ChangedValue.tooltipCritness = value;
				}
			}
		}
		public bool CSOCompat {
			get => Value.csoCompat;
			set {
				if (Value.csoCompat != value) {
					ChangedValue.csoCompat = value;
				}
			}
		}
		[DisplayConfigValuesFilter<DamageClassDefinition>(typeof(ColoredDamageTypesOptionsConfigElement), nameof(IsValidPreset))]
		public ColorDataDefinition SelectedPreset {
			get => Value.selectedPreset;
			set {
				if (Value.selectedPreset != value) {
					ChangedValue.selectedPreset = value;
					needsRefresh = true;
				}
			}
		}
		public bool IsValidPreset(ColorDataDefinition newType) {
			if (newType.Equals(SelectedPreset)) return true;
			return Value.isRemoteColors || newType.ColorData.ShowInOwnColors;
		}
		public bool Interpolated {
			get => SelectedColorSet.interpolated;
			set {
				if (SelectedColorSet.interpolated != value) {
					ChangedValue.SelectedColorSet.interpolated = value;
					needsRefresh = true;
				}
			}
		}
		[Increment(0.02f)]
		public float InterpolationMode {
			get => SelectedColorSet.interpolationMode;
			set {
				if (SelectedColorSet.interpolationMode != value) {
					ChangedValue.SelectedColorSet.interpolationMode = value;
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
		PropertyFieldWrapper GetProperty(string name) => new(GetType().GetProperty(name).WithCanWrite(!SelectedColorSet.ReadOnly));
		protected void SetupList() {
			if (ValueChanged) Value.SelectedColorSet.ValidatePriorityOrder();
			list.Clear();
			int index = 0;
			if (!Value.isRemoteColors) {
				ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(ApplyToTooltips))), this, index++);
				if (ApplyToTooltips) ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(TooltipCritness))), this, index++);
				if (ApplyToTooltips && ColoredDamageTypesOptions.CSOEnabled) ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(CSOCompat))), this, index++);
			}
			ConfigManager.WrapIt(list, ref height, new(GetType().GetProperty(nameof(SelectedPreset))), this, index++);
			foreach (KeyValuePair<DamageClassDefinition, DamageTypeData> color in Value.SelectedColorSet.ColorSet) {
				ColorsElement colorsElement = new(color.Key, color.Value, Value.SelectedColorSet.ReadOnly);
				colorsElement.Top.Pixels += height;
				list.Add(colorsElement);
				height += colorsElement.height;
				DamageClassDefinition origKey = color.Key;
				if (!SelectedColorSet.ReadOnly) colorsElement.SetValue += (@class, colors) => {
					ChangedValue.SelectedColorSet.ColorSet.Remove(origKey);
					if (@class is not null && colors is not null) ChangedValue.SelectedColorSet.ColorSet.Add(@class, colors);
					else needsRefresh = true;
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
					needsRefresh = true;
				};
				list.Add(addButton);
				index++;
			}
			if (SelectedColorSet.ShowExtrapolationData) {
				ConfigManager.WrapIt(list, ref height, GetProperty(nameof(Interpolated)), this, index++);
				if (!Interpolated) {
					PropertyFieldWrapper memberInfo = GetProperty(nameof(PriorityOrder));
					ConfigManager.WrapIt(list, ref height, memberInfo, this, index++);
				} else {
					ConfigManager.WrapIt(list, ref height, GetProperty(nameof(InterpolationMode)), this, index++);
				}
			}
			if (SelectedColorSet.ReadOnly && !SelectedColorSet.IsSpecial) {
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
			if (needsRefresh.TrySet(false)) {
				HashSet<DamageClassDefinition> opened = [];
				foreach (ColorsElement item in list._items.TryCast<ColorsElement>()) if (item.opened) opened.Add(item.Type);
				SetupList();
				foreach (ColorsElement item in list._items.TryCast<ColorsElement>()) item.opened = opened.Contains(item.Type);
			}
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
}
