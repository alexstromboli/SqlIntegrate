using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CodeTypes;

public record EnumValue (string Name, int Value);

[Flags]
public enum NamingFlags
{
	None = 0,
	UseAliases = 1,
	ShowParameterNames = 2,
	QualifyParameterNames = 4
}

public class TypeLike
{
	protected static readonly NullabilityInfoContext NullabilityInfoContext = new();

	public bool IsEnum => EnumValues.Length > 0;
	public EnumValue[] EnumValues { get; }
	public bool IsValueType { get; }
	public bool IsArray { get; }

	public TypeLike Core { get; }

	protected Lazy<TypeLike?> BaseTypeImpl { get; }
	public TypeLike? BaseType => BaseTypeImpl.Value;

	protected Lazy<TypeLike?> DeclaringTypeImpl { get; }
	public TypeLike? DeclaringType => DeclaringTypeImpl.Value;

	protected Lazy<TypeLike?> DeclaringTypeArgumentedImpl { get; }
	public TypeLike? DeclaringTypeArgumented => DeclaringTypeArgumentedImpl.Value;

	public int OwnGenericArgumentsSkip { get; }
	public int OwnGenericArgumentsCount { get; }

	public bool IsGenericParameter { get; }
	public bool IsGenericType => GenericTypeArguments.Length > 0 || GenericTypeParameters.Length > 0;

	public bool IsGenericTypeDefinition => GenericTypeParameters.Length > 0;
	protected Lazy<TypeLike?> GenericTypeDefinitionImpl { get; }
	public TypeLike? GenericTypeDefinition => GenericTypeDefinitionImpl.Value;
	public TypeLike? FirstGenericArgument => GenericTypeArguments.Length > 0 ? GenericTypeArguments[0] : null;

	public string Name { get; }
	public string Namespace { get; }
	protected Lazy<string> UniqueNameImpl { get; }
	public string UniqueName => UniqueNameImpl.Value;

	public Type? ClrType { get; }
	protected Lazy<TypeLike> NullableImpl { get; }
	public TypeLike Nullable => NullableImpl.Value;
	public bool IsForceNullable { get; }
	protected Lazy<TypeLike> ForceNullableImpl { get; }
	public TypeLike ForceNullable => ForceNullableImpl.Value;
	public TypeLike NullCastable => Core.Nullable;

	public TypeLike? ElementType { get; }
	public TypeLike[] GenericTypeArguments { get; }
	public TypeLike[] GenericTypeParameters { get; }

	public bool CanBeNull => IsForceNullable || !IsValueType || (ClrType != null && (ClrType?.IsNullableT () ?? false));

	public bool IsVoid => ClrType == typeof(void);
	public bool IsTask => ClrType == typeof(Task) || (IsGenericType &&
	                                                  (ClrType?.GetGenericTypeDefinition () == typeof(Task<>) ||
	                                                   ClrType?.GetGenericTypeDefinition () == typeof(ValueTask<>)));
	protected Lazy<TypeLike> TaskOfTypeImpl { get; set; }
	public TypeLike TaskOfType => TaskOfTypeImpl.Value;
	protected Lazy<TypeLike> ValueTaskOfTypeImpl { get; set; }
	public TypeLike ValueTaskOfType => ValueTaskOfTypeImpl.Value;
	protected Lazy<TypeLike?> TaskedTypeImpl { get; set; }
	public TypeLike? TaskedType => TaskedTypeImpl.Value;

	public TypeLike (
		string? FullName,
		bool IsValueType = false,
		bool IsForceNullable = false,
		bool IsArray = false,
		TypeLike? ElementType = null,
		bool IsGenericParameter = false,
		TypeLike[]? GenericTypeArguments = null,
		string[]? GenericTypeParameters = null,
		TypeLike? GenericTypeDefinition = null,
		TypeLike? BaseType = null,
		TypeLike? DeclaringType = null,
		EnumValue[]? EnumValues = null,
		TypeLike? Core = null
	)
	{
		this.Core = Core ?? this;

		if (IsForceNullable)
		{
			this.Core = Core ?? new TypeLike (
				FullName,
				IsValueType,
				IsForceNullable: false,
				IsArray,
				ElementType,
				IsGenericParameter,
				GenericTypeArguments,
				GenericTypeParameters,
				GenericTypeDefinition,
				BaseType,
				DeclaringType,
				EnumValues,
				Core: null
			);
			FullName = null;
		}

		//
		GenericTypeParameters ??= [];
		GenericTypeArguments ??= [];

		if (IsArray)
		{
			Assert (ElementType != null);
			Assert (FullName == null);
			Assert (!IsValueType);
			Assert (!IsGenericParameter);
			Assert (GenericTypeArguments.Length == 0);
			Assert (GenericTypeParameters.Length == 0);
			Assert (GenericTypeDefinition == null);
			Assert (DeclaringType == null);
			Assert (EnumValues == null || EnumValues.Length == 0);

			this.Name = "";
			this.Namespace = "";
			this.OwnGenericArgumentsSkip = 0;
			this.OwnGenericArgumentsCount = 0;
		}
		else
		{
			Assert (FullName != null || IsForceNullable);

			Assert (!IsGenericParameter || GenericTypeParameters.Length == 0 && GenericTypeArguments.Length == 0);
			Assert (!IsGenericParameter || !FullName!.Contains ('.'));
			Assert (GenericTypeParameters.Length == 0 || GenericTypeArguments.Length == 0);
			Assert (GenericTypeArguments.Length == 0 || GenericTypeDefinition != null);
			Assert (GenericTypeDefinition == null || GenericTypeDefinition.GenericTypeParameters.Length == GenericTypeArguments.Length);

			(this.OwnGenericArgumentsSkip, this.OwnGenericArgumentsCount) =
				MakeSkipCount (GenericTypeParameters, GenericTypeDefinition, DeclaringType);

			if (FullName != null)
			{
				string[] NameComponents = FullName!.Split ('.');
				this.Name = TypeLikeUtils.ChopPureGenericFullName (NameComponents[^1]);
				this.Namespace = DeclaringType != null || NameComponents.Length == 0
					? ""
					: string.Join ('.', NameComponents[..^1]);
			}
			else
			{
				this.Name = "";
				this.Namespace = "";
			}

			if (IsForceNullable && IsValueType)
			{
				Assert (ElementType == null);
				Assert (!IsGenericParameter);
				Assert (GenericTypeParameters.Length == 0);

				GenericTypeArguments = [Core];
				this.ClrType = typeof(Nullable<>);
				BaseType = new TypeLike (this.ClrType.BaseType!);
				DeclaringType = null;
			}
		}

		this.BaseTypeImpl =
			new Lazy<TypeLike?> (() => BaseType ?? new TypeLike (IsValueType ? typeof(ValueType) : typeof(object)));
		this.DeclaringTypeImpl = new Lazy<TypeLike?> (DeclaringType);
		this.DeclaringTypeArgumentedImpl = new Lazy<TypeLike?> (() =>
			DeclaringType == null || DeclaringType.GenericTypeParameters.Length > 0
				? null
				: DeclaringType.IsGenericType
					? DeclaringType.MakeGenericType (GenericTypeArguments.Take (OwnGenericArgumentsSkip).ToArray ())
					: DeclaringType
		);

		this.IsArray = IsArray;
		this.ElementType = ElementType;
		this.IsValueType = IsValueType;
		this.IsForceNullable = IsForceNullable;
		// not tested for generic types
		this.IsGenericParameter = IsGenericParameter;
		this.GenericTypeDefinitionImpl = new Lazy<TypeLike?> (GenericTypeDefinition);
		this.GenericTypeArguments = GenericTypeArguments!;
		this.GenericTypeParameters = GenericTypeParameters!.Select (pn => new TypeLike (
			pn, IsGenericParameter: true, DeclaringType: this
			)).ToArray ();

		if (EnumValues is { Length: > 0 })
		{
			this.EnumValues = EnumValues;
		}
		else
		{
			this.EnumValues = [];
		}

		this.TaskOfTypeImpl = new Lazy<TypeLike> (() => new TypeLike (typeof(Task<>)).MakeGenericType ([this]));
		this.ValueTaskOfTypeImpl = new Lazy<TypeLike> (() => IsValueType
			? new TypeLike (typeof(ValueTask<>)).MakeGenericType ([this])
			: this.TaskOfType);
		this.TaskedTypeImpl = new Lazy<TypeLike?> (() => null);

		(NullableImpl, ForceNullableImpl) = MakeNullables (this);

		UniqueNameImpl = new Lazy<string> (MakeUniqueName);
	}

	protected static (int OwnGenericArgumentsSkip, int OwnGenericArgumentsCount)
		MakeSkipCount<T> (T[] GenericTypeParameters, TypeLike? GenericTypeDefinition, TypeLike? DeclaringType)
	{
		int OwnGenericArgumentsSkip = 0;
		int OwnGenericArgumentsCount = 0;

		if (GenericTypeDefinition != null)
		{
			OwnGenericArgumentsSkip = GenericTypeDefinition.OwnGenericArgumentsSkip;
			OwnGenericArgumentsCount = GenericTypeDefinition.OwnGenericArgumentsCount;
		}
		else if (GenericTypeParameters.Length > 0)
		{
			if (DeclaringType == null)
			{
				OwnGenericArgumentsSkip = 0;
			}
			else
			{
				OwnGenericArgumentsSkip = DeclaringType.OwnGenericArgumentsSkip + DeclaringType.OwnGenericArgumentsCount;
			}

			OwnGenericArgumentsCount = GenericTypeParameters.Length - OwnGenericArgumentsSkip;
		}

		return (OwnGenericArgumentsSkip, OwnGenericArgumentsCount);
	}

	protected static (int OwnGenericArgumentsSkip, int OwnGenericArgumentsCount) MakeSkipCount (Type ClrType)
	{
		int OwnGenericArgumentsSkip = 0;
		int OwnGenericArgumentsCount = 0;

		if (ClrType.IsGenericType)
		{
			int TotalLength = ClrType.GetGenericArguments ().Length;
			if (ClrType.DeclaringType == null)
			{
				OwnGenericArgumentsSkip = 0;
				OwnGenericArgumentsCount = TotalLength;
			}
			else
			{
				var Parent = MakeSkipCount (ClrType.DeclaringType);
				OwnGenericArgumentsSkip = Parent.OwnGenericArgumentsSkip + Parent.OwnGenericArgumentsCount;
				OwnGenericArgumentsCount = TotalLength - OwnGenericArgumentsSkip;
			}
		}

		return (OwnGenericArgumentsSkip, OwnGenericArgumentsCount);
	}

	protected static (Lazy<TypeLike> NullableImpl, Lazy<TypeLike> ForceNullableImpl) MakeNullables (TypeLike This)
	{
		Lazy<TypeLike> NullableImpl = new Lazy<TypeLike> (() => This.IsForceNullable
			? This
			: This.Core.CanBeNull
				? This.Core
				: This.Core.MakeForceNullable ()
		);

		Lazy<TypeLike> ForceNullableImpl = new Lazy<TypeLike> (() => This.MakeForceNullable ());

		return (NullableImpl, ForceNullableImpl);
	}

	protected static void Assert (bool B)
	{
		if (!B)
		{
			throw new ArgumentException ("Wrong argument");
		}
	}

	public TypeLike (Type ClrType, bool IsForceNullable = false, TypeLike? ElementType = null,
		TypeLike[]? GenericTypeArguments = null)
	{
		this.ClrType = ClrType;

		this.GenericTypeArguments = GenericTypeArguments
		                            ?? this.ClrType.GenericTypeArguments
			                            .ToTypeLikeArray ();
		this.GenericTypeParameters = this.ClrType.IsGenericTypeDefinition
			? this.ClrType.GetGenericArguments ()
				.ToTypeLikeArray ()
			: [];

		(Name, Namespace, IsValueType, EnumValues, IsGenericParameter, BaseTypeImpl,
				DeclaringTypeImpl, DeclaringTypeArgumentedImpl,
				GenericTypeDefinitionImpl,
				TaskOfTypeImpl, ValueTaskOfTypeImpl, TaskedTypeImpl) =
			MakeProperties (this.ClrType, this, this.GenericTypeArguments);

		(this.OwnGenericArgumentsSkip, this.OwnGenericArgumentsCount) = MakeSkipCount (this.ClrType);

		this.IsForceNullable = IsForceNullable || ClrType.IsNullableT ();
		this.ElementType = ElementType;
		this.IsArray = ClrType.IsArray;

		if (ElementType == null)
		{
			Type? tEl = TypeLikeUtils.GetCollectionItemType (this.ClrType);
			if (tEl != null)
			{
				this.ElementType = new TypeLike (tEl);
			}
		}

		//
		this.Core = this;
		if (this.IsForceNullable)
		{
			this.Core = new TypeLike (System.Nullable.GetUnderlyingType (ClrType) ?? ClrType, false, ElementType,
				GenericTypeArguments);
		}

		(NullableImpl, ForceNullableImpl) = MakeNullables (this);

		//
		UniqueNameImpl = new Lazy<string> (MakeUniqueName);
	}

	public TypeLike MakeForceNullable (bool NeedForceNullable = true)
	{
		Assert (!IsGenericParameter);
		Assert (GenericTypeParameters.Length == 0);

		if (!NeedForceNullable)
		{
			return Core;
		}

		if (this.IsForceNullable == NeedForceNullable)
		{
			return this;
		}

		if (ClrType != null)
		{
			return new TypeLike (ClrType, NeedForceNullable, ElementType, GenericTypeArguments);
		}

		return new TypeLike (null, IsValueType, NeedForceNullable,
			IsArray, ElementType,
			IsGenericParameter,
			GenericTypeArguments, null,
			GenericTypeDefinition,
			BaseType, DeclaringType,
			EnumValues, this);
	}

	public TypeLike RemoveForceNullable ()
	{
		return Core;
	}

	public TypeLike (NullabilityInfo NullabilityInfo)
	{
		this.ClrType = NullabilityInfo.Type;

		this.GenericTypeArguments =
			this.ClrType.IsNullableT ()
				? this.ClrType.GenericTypeArguments.ToTypeLikeArray ()
				: NullabilityInfo.GenericTypeArguments.ToTypeLikeArray ();
		this.GenericTypeParameters = [];

		(Name, Namespace, IsValueType, EnumValues, IsGenericParameter, BaseTypeImpl,
				DeclaringTypeImpl, DeclaringTypeArgumentedImpl,
				GenericTypeDefinitionImpl,
				TaskOfTypeImpl, ValueTaskOfTypeImpl, TaskedTypeImpl) =
			MakeProperties (this.ClrType, this, this.GenericTypeArguments);

		(this.OwnGenericArgumentsSkip, this.OwnGenericArgumentsCount) = MakeSkipCount (this.ClrType);

		this.IsForceNullable = NullabilityInfo.WriteState == NullabilityState.Nullable;
		this.IsArray = this.ClrType.IsArray;
		this.ElementType = NullabilityInfo.ElementType == null ? null : new TypeLike (NullabilityInfo.ElementType);

		//
		this.Core = this;
		if (this.IsForceNullable)
		{
			this.Core = new TypeLike (System.Nullable.GetUnderlyingType (ClrType) ?? ClrType, false, ElementType,
				GenericTypeArguments);
		}

		(NullableImpl, ForceNullableImpl) = MakeNullables (this);

		//
		UniqueNameImpl = new Lazy<string> (MakeUniqueName);
	}

	private static (string Name, string Namespace,
		bool IsValueType,
		EnumValue[] EnumValues,
		bool IsGenericParameter,
		Lazy<TypeLike?> BaseTypeImpl,
		Lazy<TypeLike?> DeclaringTypeImpl, Lazy<TypeLike?> DeclaringTypeArgumentedImpl,
		Lazy<TypeLike?> GenericTypeDefinitionImpl,
		Lazy<TypeLike> TaskOfTypeImpl, Lazy<TypeLike> ValueTaskOfTypeImpl, Lazy<TypeLike?> TaskedTypeImpl)
		MakeProperties (Type ClrType, TypeLike TypeLike, TypeLike[] GenericTypeArguments)
	{
		string Name = TypeLikeUtils.ChopPureGenericFullName (ClrType.Name);
		string Namespace = ClrType.DeclaringType == null ? ClrType.Namespace! : "";
		bool IsValueType = ClrType.IsValueType;

		var BaseTypeImpl = new Lazy<TypeLike?> (() => ClrType.BaseType == null ? null : new TypeLike (ClrType.BaseType!));
		var DeclaringTypeImpl = new Lazy<TypeLike?> (() =>
			ClrType.DeclaringType == null ? null : new TypeLike (ClrType.DeclaringType!));
		var DeclaringTypeArgumentedImpl = new Lazy<TypeLike?> (() =>
			ClrType.DeclaringType == null
			? null
			: ClrType.IsGenericType
			? DeclaringTypeImpl.Value!.MakeGenericType (GenericTypeArguments.Take (DeclaringTypeImpl.Value.ClrType!.GetGenericArguments ().Length).ToArray ())
			: DeclaringTypeImpl.Value
		);

		EnumValue[] EnumValues;
		if (ClrType.IsEnum)
		{
			EnumValues = ClrType.GetEnumValues ()
				.Cast<object> ()
				.Select (v => new EnumValue (v.ToString ()!, (int)v))
				.ToArray ();
		}
		else
		{
			EnumValues = [];
		}

		Lazy<TypeLike?> GenericTypeDefinitionImpl = new(() =>
			ClrType.IsGenericType ? new TypeLike (ClrType.GetGenericTypeDefinition ()) : null);

		bool IsGenericParameter = ClrType.IsGenericParameter;

		var TaskOfTypeImpl = new Lazy<TypeLike> (() => ClrType == typeof (void)
			? new TypeLike (typeof(Task))!
			: new TypeLike (typeof(Task<>)).MakeGenericType ([TypeLike]));
		var ValueTaskOfTypeImpl = new Lazy<TypeLike> (() => ClrType.IsValueType && ClrType != typeof (void)
			? new TypeLike (typeof(ValueTask<>)).MakeGenericType ([TypeLike])
			: TaskOfTypeImpl.Value);
		var TaskedTypeImpl = new Lazy<TypeLike?> (() => ClrType == typeof(Task)
			? new TypeLike (typeof(void))
			: ClrType.IsGenericType
				? ClrType?.GetGenericTypeDefinition () == typeof(Task<>) ||
				  ClrType?.GetGenericTypeDefinition () == typeof(ValueTask<>)
					? TypeLike.GenericTypeArguments[0]
					: null
				: null);

		return (Name, Namespace, IsValueType, EnumValues, IsGenericParameter, BaseTypeImpl, DeclaringTypeImpl,
			DeclaringTypeArgumentedImpl, GenericTypeDefinitionImpl,
			TaskOfTypeImpl, ValueTaskOfTypeImpl, TaskedTypeImpl);
	}

	public TypeLike (FieldInfo TypedItemInfo)
	: this (NullabilityInfoContext.Create (TypedItemInfo))
	{
	}

	public TypeLike (PropertyInfo TypedItemInfo)
	: this (NullabilityInfoContext.Create (TypedItemInfo))
	{
	}

	public TypeLike (ParameterInfo TypedItemInfo)
	: this (NullabilityInfoContext.Create (TypedItemInfo))
	{
	}

	public override string ToString ()
	{
		return UniqueName;
	}

	public TypeLike MakeArray ()
	{
		if (IsGenericTypeDefinition)
		{
			throw new InvalidOperationException ("Cannot make an array of generic type definition.");
		}

		if (ClrType != null)
		{
			return new TypeLike (ClrType.MakeArrayType (), false, this);
		}

		return new TypeLike (null, false, false, true, this);
	}

	public TypeLike MakeGenericType (TypeLike[] GenericTypeArguments, bool IsForceNullable = false)
	{
		if (!IsGenericType)
		{
			return this;
		}

		TypeLike Definition = IsGenericTypeDefinition ? this : this.GenericTypeDefinition!;
		int Length = Definition.GenericTypeParameters.Length;
		TypeLike[] CutMin = GenericTypeArguments.Take (Length).ToArray ();

		if (Definition.ClrType != null && CutMin.All (t => t.ClrType != null))
		{
			Type[] Cut = CutMin.Select (gta => gta.ClrType!).ToArray ();
			Type NewClrType = Definition.ClrType.MakeGenericType (Cut);

			return new TypeLike (NewClrType, IsForceNullable, null, CutMin);
		}

		return new TypeLike ((string.IsNullOrWhiteSpace (Namespace) ? "" : Namespace + ".") + Name,
			IsValueType, IsForceNullable, false, null, false,
			CutMin, null, Definition,
			BaseType, // here: BaseType may need to be produced too
			Definition.DeclaringType, null
		);
	}

	public string MakeUniqueName ()
	{
		NamingFlags Flags = NamingFlags.UseAliases;
		if (IsGenericParameter)
		{
			Flags |= NamingFlags.QualifyParameterNames;
		}

		return ProduceName (CodeContext.Simple, Flags);
	}

	public string ProduceName (CodeContext Context, NamingFlags Flags = NamingFlags.UseAliases)
	{
		if (IsGenericParameter)
		{
			if ((Flags & NamingFlags.QualifyParameterNames) != 0)
			{
				return DeclaringType!.ProduceName (Context,
					Flags & ~NamingFlags.ShowParameterNames & ~NamingFlags.QualifyParameterNames)
				       + ".[" + Name + "]";
			}

			return Name;
		}

		if ((Flags & NamingFlags.UseAliases) != 0)
		{
			string NoAlias = ProduceName (CodeContext.FullNames, Flags & ~NamingFlags.UseAliases);
			if (Context.FullNamesToAliases.TryGetValue (NoAlias, out var Alias))
			{
				return Alias;
			}
		}

		if (ClrType?.IsNullableT () ?? false)
		{
			return FirstGenericArgument!.ProduceName (Context, Flags) + "?";
		}

		if (IsForceNullable)
		{
			return RemoveForceNullable ().ProduceName (Context, Flags) + "?";
		}

		if (IsArray)
		{
			return ElementType!.ProduceName (Context, Flags) + "[]";
		}

		string NamespacePrefix = "";
		if (DeclaringType == null && !string.IsNullOrWhiteSpace (Namespace) && !Context.Usings.Contains (Namespace!))
		{
			NamespacePrefix = Namespace + ".";
		}
		else if (DeclaringType != null)
		{
			NamespacePrefix = (IsGenericTypeDefinition ? DeclaringType : DeclaringTypeArgumented)!
				.ProduceName (Context, Flags) + ".";
		}

		string Result = NamespacePrefix
		                + Name
		                + (!IsGenericType || OwnGenericArgumentsCount == 0
			                ? ""
			                : "<"
			                  + string.Join (',',
				                  (IsGenericTypeDefinition ? GenericTypeParameters : GenericTypeArguments)
				                  .Skip (OwnGenericArgumentsSkip)
				                  .Take (OwnGenericArgumentsCount)
				                  .Select (
					                  gta => !IsGenericTypeDefinition || (Flags & NamingFlags.ShowParameterNames) != 0
						                  ? gta.ProduceName (Context, Flags)
						                  : ""))
			                  + ">");

		return Result;
	}
}
