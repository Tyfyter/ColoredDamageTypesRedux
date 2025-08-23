using Microsoft.Xna.Framework;
using Newtonsoft.Json;
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
			ColorSet[DamageClass.Melee] = new(Color.Firebrick, Color.Red);
			ColorSet[DamageClass.Ranged] = new(Color.SeaGreen, Color.Lime);
			ColorSet[DamageClass.Magic] = new(Color.DodgerBlue, Color.Blue);
			ColorSet[DamageClass.Summon] = new(Color.Goldenrod, Color.Orange);
		}
	}
	[Autoload(false)]
	public class ExternalColorData : ColorData {
		public ExternalColorData(Dictionary<string, (Color hitColor, Color critColor)> colors) {
			foreach (KeyValuePair<string, (Color hitColor, Color critColor)> item in colors) {
				ColorSet[new(item.Key)] = new(item.Value.hitColor, item.Value.critColor);
			}
		}
	}
	[CustomModConfigItem(typeof(NamedDefinitionConfigElement<ColorDataDefinition>))]
	[TypeConverter(typeof(ToFromStringConverter<ColorDataDefinition>))]
	[JsonConverter(typeof(JsonConverter))]
	public class ColorDataDefinition : EntityDefinition, IEquatable<ColorDataDefinition>, INamedDefinition, IEnumerableDefinition<ColorDataDefinition> {
		public static readonly Func<TagCompound, ColorDataDefinition> DESERIALIZER = Load;
		public override bool IsUnloaded => ColorData is null;
		public string FullName => $"{Mod}/{Name}";
		public const string custom_colors = $"{nameof(ColoredDamageTypesRedux)}/{nameof(CustomColorData)}";
		public ColorData ColorData => FullName == custom_colors ? ColoredDamageTypesReduxConfig.Instance.options.CustomColors
			: (ColoredDamageTypesRedux.loadedColorDatas.TryGetValue(FullName, out ColorData data) ? data : null);
		public override int Type => -1;
		public ColorDataDefinition() : base() { }
		public ColorDataDefinition(string key) : base(key) { }
		public ColorDataDefinition(string mod, string name) : base(mod, name) { }
		public static ColorDataDefinition FromString(string s) => new(s);
		public static ColorDataDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));
		public bool Equals(ColorDataDefinition other) => other?.FullName == FullName;
		public override string DisplayName => IsUnloaded ? Language.GetTextValue("Mods.ModLoader.Unloaded") : ColorData.DisplayName.Value;
		public override bool Equals(object obj) => Equals(obj as ColorDataDefinition);
		public override int GetHashCode() => FullName.GetHashCode();
		public class JsonConverter : Newtonsoft.Json.JsonConverter {
			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
				return FromString(reader.Value.ToString());
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
				switch (writer.WriteState) {
					case WriteState.Property:
					case WriteState.Array:
					writer.WriteValue(value.ToString());
					break;
					default:
					writer.WriteRaw(value.ToString());
					break;
				}
			}
			public override bool CanConvert(Type objectType) => objectType == typeof(ColorDataDefinition);
		}
		public static IEnumerable<ColorDataDefinition> GetOptions() {
			yield return new(custom_colors);
			foreach (string id in ColoredDamageTypesRedux.loadedColorDatas.Keys) {
				yield return new(id);
			}
		}
		public bool ShowInternalName => false;
	}
}
