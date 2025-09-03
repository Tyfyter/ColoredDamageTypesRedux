using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using PegasusLib.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace ColoredDamageTypesRedux {
	public abstract class ColorData : ILoadable {
		[JsonIgnore]
		public Mod Mod { get; protected set; }
		[JsonIgnore]
		public virtual string Name => GetType().Name;
		[JsonIgnore]
		public string FullName => $"{Mod.Name}/{Name}";
		[JsonIgnore]
		public LocalizedText DisplayName => Mod.GetLocalization("ColoredDamageTypesPreset." + Name, () => Name);
		[JsonIgnore]
		public virtual bool ReadOnly => true;
		[JsonIgnore]
		public virtual bool ShowInOwnColors => true;
		[JsonIgnore]
		public virtual bool ShowExtrapolationData => !IsSpecial;
		[JsonIgnore]
		public virtual bool IsSpecial => false;
		[JsonIgnore]
		public virtual (bool interpolated, float interpolationMode) InterpolationData => (interpolated, interpolationMode);
		[JsonIgnore]
		public virtual DamageClassDefinition[] PriorityOrder => priorityOrder;
		public bool interpolated = true;
		public float interpolationMode = 0;
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
		public virtual Color? GetColor(DamageClass type, bool crit) {
			if (ColorSet.TryGetValue(new(type.Type), out DamageTypeData colors)) return crit ? colors.CritColor : colors.HitColor;
			return null;
		}
		public Color? GetFinalColor(DamageClass damageClass, bool crit) {
			damageClass = damageClass.DisplayDamageType();
			{
				if (GetColor(damageClass, crit) is Color color) return color;
			}
			if (InterpolationData.interpolated) {
				Vector4 total = Vector4.Zero;
				Vector2 sl = Vector2.Zero;
				float count = 0;
				foreach (DamageClass type in new DamageClassList()) {
					float weight = GetInterpolationWeight(damageClass, type);
					if (weight > 0 && GetColor(type, crit) is Color color) {
						total += color.ToVector4() * weight;
						Vector3 hsl = Main.rgbToHsl(color) * weight;
						sl.X += hsl.Y;
						sl.Y += hsl.Z;
						count += weight;
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
				DamageClassDefinition parent = PriorityOrder.FirstOrDefault(d => !d.IsUnloaded && damageClass.CountsAsClass(d.DamageClass));
				if (parent is not null && GetColor(parent.DamageClass, crit) is Color color) return color;
			}
			return null;
		}
		public float GetInterpolationWeight(DamageClass @for, DamageClass type) {
			return float.Lerp(@for.GetModifierInheritance(type).damageInheritance, @for.CountsAsClass(type).ToInt(), InterpolationData.interpolationMode);
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
