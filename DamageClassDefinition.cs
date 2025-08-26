using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using PegasusLib;
using PegasusLib.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ColoredDamageTypesRedux {
	[CustomModConfigItem(typeof(NamedDefinitionConfigElement<DamageClassDefinition>))]
	[TypeConverter(typeof(ToFromStringConverter<DamageClassDefinition>))]
	[JsonConverter(typeof(JsonConverter))]
	public class DamageClassDefinition : EntityDefinition, IEquatable<DamageClassDefinition>, INamedDefinition, IEnumerableDefinition<DamageClassDefinition> {
		public static readonly Func<TagCompound, DamageClassDefinition> DESERIALIZER = Load;
		public override bool IsUnloaded => !(Mod == "Terraria" && Name == "None" || Mod == "" && Name == "") && DamageClass is null;
		public string FullName => $"{Mod}/{Name}";
		public DamageClass DamageClass => ModContent.TryFind(FullName, out DamageClass @class) ? @class : null;
		public override int Type => DamageClass?.Type ?? -1;
		public DamageClassDefinition() : base() { }
		/// <summary><b>Note: </b>As ModConfig loads before other content, make sure to only use <see cref="DamageClassDefinition(string, string)"/> for modded content in ModConfig classes. </summary>
		public DamageClassDefinition(int type) : base(DamageClassLoader.GetDamageClass(type).FullName) { }
		public DamageClassDefinition(string key) : base(key) { }
		public DamageClassDefinition(string mod, string name) : base(mod, name) { }
		public static DamageClassDefinition FromString(string s) => new(s);
		public static DamageClassDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));
		public bool Equals(DamageClassDefinition other) => other?.FullName == FullName;
		public override string DisplayName => IsUnloaded || Type == -1 ? Language.GetTextValue("Mods.ModLoader.Unloaded") : DamageClass.DisplayName.Value.Trim();
		public override bool Equals(object obj) => Equals(obj as DamageClassDefinition);
		public override int GetHashCode() => FullName.GetHashCode();
		public static implicit operator DamageClassDefinition(DamageClass @class) => new(@class.Type);
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
			public override bool CanConvert(Type objectType) => objectType == typeof(DamageClassDefinition);
		}
		public static IEnumerable<DamageClassDefinition> GetOptions() {
			foreach (DamageClass @class in new DamageClassList()) {
				yield return new(@class.Type);
			}
		}
	}
	public readonly struct DamageClassList : IList<DamageClass> {
		public readonly DamageClass this[int index] {
			get => DamageClassLoader.GetDamageClass(index);
			set => throw new InvalidOperationException();
		}
		public int Count => DamageClassLoader.DamageClassCount;
		public bool IsReadOnly => true;

		public bool Contains(DamageClass item) {
			throw new NotImplementedException();
		}
		public void CopyTo(DamageClass[] array, int arrayIndex) {
			for (int i = 0; i < Count; i++) {
				array[i + arrayIndex] = this[i];
			}
		}
		public int IndexOf(DamageClass item) {
			if (Equals(item, this[item.Type])) return item.Type;
			for (int i = 0; i < Count; i++) {
				if (Equals(item, this[i])) return i;
			}
			return -1;
		}
		public IEnumerator<DamageClass> GetEnumerator() => new Enumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		struct Enumerator : IEnumerator<DamageClass> {
			public readonly DamageClass Current => DamageClassLoader.GetDamageClass(index);
			readonly object IEnumerator.Current => DamageClassLoader.GetDamageClass(index);
			int index;
			public readonly void Dispose() { }
			public bool MoveNext() => ++index < DamageClassLoader.DamageClassCount;
			public void Reset() {
				index = 0;
			}
		}
		public void Add(DamageClass item) => throw new InvalidOperationException();
		public void Clear() => throw new InvalidOperationException();
		public void Insert(int index, DamageClass item) => throw new InvalidOperationException();
		public bool Remove(DamageClass item) => throw new InvalidOperationException();
		public void RemoveAt(int index) => throw new InvalidOperationException();
	}
}
