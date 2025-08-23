using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using PegasusLib;
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
		public override string DisplayName => IsUnloaded || Type == -1 ? Language.GetTextValue("Mods.ModLoader.Unloaded") : DamageClass.DisplayName.Value;
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
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
	public class ValueFilterAttribute<TFiltered>(Type memberType, string memberName) : Attribute {
		public Predicate<TFiltered> GetFilter(object instance) {
			if (instance.GetType() == memberType && memberType.GetMethod(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(TFiltered)]) is MethodInfo instanceMethod) {
				return instanceMethod.CreateDelegate<Predicate<TFiltered>>(instance);
			}
			return memberType.GetMethod(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, [typeof(TFiltered)]).CreateDelegate<Predicate<TFiltered>>(null);
		}
	}
	public interface INamedDefinition {
		public abstract string FullName { get; }
		public abstract string DisplayName { get; }
		public bool ShowInternalName => true;
	}
	public interface IEnumerableDefinition<TDefinition> {
		public static abstract IEnumerable<TDefinition> GetOptions();
	}
	public class NamedDefinitionConfigElement<TDefinition> : ConfigElement<TDefinition> where TDefinition : EntityDefinition, INamedDefinition, IEnumerableDefinition<TDefinition> {
		protected bool pendingChanges = false;
		public override void OnBind() {
			base.OnBind();
			base.TextDisplayFunction = TextDisplayOverride ?? base.TextDisplayFunction;
			pendingChanges = true;
			normalTooltip = TooltipFunction?.Invoke() ?? string.Empty;
			TooltipFunction = () => tooltip;
		}
		public override void OnInitialize() {
			base.OnInitialize();
			SetupList();
		}
		public Func<string> TextDisplayOverride { get; set; }
		float height = 0;
		bool opened = false;
		string normalTooltip;
		string tooltip = string.Empty;
		protected void SetupList() {
			RemoveAllChildren();
			Recalculate();
		}
		public override void LeftClick(UIMouseEvent evt) {
			opened = true;
			RemoveAllChildren();
			height = 30;
			Predicate<TDefinition>[] filters = MemberInfo.MemberInfo.GetCustomAttributes<ValueFilterAttribute<TDefinition>>().Select(a => a.GetFilter(Parent?.Parent?.Parent is UIList ? Parent.Parent.Parent.Parent : Parent)).ToArray();
			foreach (TDefinition option in TDefinition.GetOptions()) {
				bool skip = false;
				foreach (Predicate<TDefinition> filter in filters) {
					if (!filter(option)) {
						skip = true;
						break;
					}
				}
				if (skip) continue;
				string text = option.DisplayName.Trim();
				Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
				UIPanel panel = new() {
					Left = new(0, 0),
					Top = new(height + 4, 0),
					Width = new(-8, 1),
					Height = new(size.Y + 4, 0),
					HAlign = 0.5f,
					PaddingTop = 0
				};
				UIText element = new(text, 0.8f) {
					Width = new(0, 1),
					Top = new(0, 0.5f),
					VAlign = 0.5f
				};
				panel.OnUpdate += element => {
					if (element is not UIPanel panel) return;
					if (panel.IsMouseHovering) {
						panel.BackgroundColor = UICommon.DefaultUIBlue;
						tooltip = option.FullName;
					} else {
						panel.BackgroundColor = UICommon.MainPanelBackground;
					}
				};
				panel.OnLeftClick += (_, _) => {
					if (Value.FullName != option.FullName) {
						Value = option;
						OnSet?.Invoke(Value);
					}
					opened = false;
					SetupList();
				};
				element.TextColor = Value.FullName == option.FullName ? Color.Goldenrod : Color.White;
				panel.Append(element);
				Append(panel);
				height += size.Y + 8;
			}
			SetHeight();
		}
		public event Action<TDefinition> OnSet;
		public override void Update(GameTime gameTime) {
			SetHeight();
			tooltip = normalTooltip;
			if (opened) base.Update(gameTime);
		}
		void SetHeight() {
			float targetHeight = opened ? height : 32;
			if (Height.Pixels != targetHeight) {
				Height.Pixels = targetHeight;
				Parent.Height.Pixels = targetHeight;
				this.Recalculate();
				Parent.Recalculate();
			}
		}
		public override void Draw(SpriteBatch spriteBatch) {
			if (opened) {
				base.Draw(spriteBatch);
			} else {
				DrawSelf(spriteBatch);
				string text = $"{Value.DisplayName.Trim() ?? ""} ({Value.FullName})";
				Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
				CalculatedStyle innerDimensions = GetInnerDimensions();
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					FontAssets.MouseText.Value,
					text,
					innerDimensions.Position() + new Vector2(innerDimensions.Width - size.X, (innerDimensions.Height - size.Y) * 0.5f + 4),
					Color.White,
					0f,
					Vector2.Zero,
					Vector2.One * 0.8f
				);
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
