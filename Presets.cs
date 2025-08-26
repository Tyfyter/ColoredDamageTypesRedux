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
			ColorSet[DamageClass.Summon] = new(new(179, 150, 36), new(255, 183, 0));
		}
	}
	public class PillarsPreset : ColorData {
		public PillarsPreset() {
			ColorSet[DamageClass.Melee] = new(new(254, 121, 2), new(253, 62, 3));
			ColorSet[DamageClass.Ranged] = new(new(34, 221, 151), new(33, 160, 141));
			ColorSet[DamageClass.Magic] = new(new(254, 126, 229), new(255, 31, 174));
			ColorSet[DamageClass.Summon] = new(new(136, 226, 255), new(14, 154, 230));
			ColorSet[DamageClass.Throwing] = new(new(161, 114, 74), new(175, 165, 103));
		}
	}
	[Autoload(false)]
	public class ExternalColorData : ColorData {
		string name;
		public override string Name => name;
		public ExternalColorData(Mod mod, string name, Dictionary<string, (Color hitColor, Color critColor)> colors) {
			Mod = mod;
			this.name = name;
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
		public static bool ShowInternalName => false;
	}
}
