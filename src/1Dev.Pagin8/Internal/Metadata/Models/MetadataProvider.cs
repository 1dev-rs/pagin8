using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Utils;
using Tar.Rest.LibShared.Internal.Exceptions;

namespace _1Dev.Pagin8.Internal.Metadata.Models;
public class MetadataProvider : IMetadataProvider
{
    private const string NoFilterFlag = "no-filter";
    private const string NoSortFlag = "no-sort";
    private const string ArrayFlag = "array";
    private const int MaxSerializationDepth = 1;

    // Shared across all instances — entity types and their attributes never change at runtime.
    private static readonly BindingFlags LookupFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;
    private static readonly ConcurrentDictionary<TypeNameKey, PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<Type, string> EntityKeyCache = new();
    private static readonly ConcurrentDictionary<TypeNameKey, bool> FilterableCache = new();
    private static readonly ConcurrentDictionary<TypeNameKey, bool> SortableCache = new();
    private static readonly ConcurrentDictionary<TypeNameKey, bool> InMetaCache = new();
    private static readonly ConcurrentDictionary<TypeNameKey, bool> NullAllowedCache = new();
    private static readonly ConcurrentDictionary<TypeNameKey, TypeCode> TypeCodeCache = new();

    private static PropertyInfo? GetCachedProperty(Type type, string propertyName)
        => PropertyCache.GetOrAdd(
            new TypeNameKey(type, propertyName),
            static key => key.Type.GetProperty(key.Name, LookupFlags));

    public IEnumerable<ColumnMetadata> Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(Type entityType, int currentDepth = 0) where TNonFilterableAttribute : Attribute where TNonSortable : Attribute where TMetaExclude : Attribute
    {
        return entityType.GetProperties()
            .Where(x => x.GetCustomAttribute<TMetaExclude>() == null)
            .Select(x => CreateColumnMetadata<TNonFilterableAttribute, TNonSortable, TMetaExclude>(x, currentDepth))
            .OrderBy(meta => meta.Name);
    }

    private ColumnMetadata CreateColumnMetadata<TNonFilterableAttribute, TNonSortable, TMetaExclude>(PropertyInfo property, int currentDepth)
        where TNonFilterableAttribute : Attribute
        where TNonSortable : Attribute
        where TMetaExclude : Attribute
    {
        var columnName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var flags = new List<string>();
        if (property.GetCustomAttribute<TNonFilterableAttribute>() != null)
        {
            flags.Add(NoFilterFlag);
        }
        if (property.GetCustomAttribute<TNonSortable>() != null)
        {
            flags.Add(NoSortFlag);
        }

        List<ColumnMetadata> nestedProperties = [];
        var arrayInnerType = GetIEnumerableElementType(propertyType);

        var effectiveType = arrayInnerType ?? propertyType;
        var typeCode = Type.GetTypeCode(effectiveType);
        var typeStr = GetTypeString(typeCode);

        if (arrayInnerType != null)
        {
            flags.Add(ArrayFlag);
            if (arrayInnerType != typeof(string) && currentDepth < MaxSerializationDepth)
            {
                var nextDepth = currentDepth + 1;
                nestedProperties = Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(arrayInnerType, nextDepth).ToList();
            }
        }
        else if (propertyType.IsClass && propertyType != typeof(string) && currentDepth < MaxSerializationDepth)
        {
            var nextDepth = currentDepth + 1;
            nestedProperties = Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(propertyType, nextDepth).ToList();
        }

        return new ColumnMetadata(columnName, typeStr, flags) { Properties = nestedProperties };
    }

    private static Type? GetIEnumerableElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType()!;
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)) return type.GetGenericArguments()[0];
        return null;
    }

    public IEnumerable<PropertyInfo> GetStringProperties<TExcludeAttribute>(Type entityType) where TExcludeAttribute : Attribute
    {
        if (entityType.IsDefined(typeof(TExcludeAttribute))) return [];

        return entityType.GetProperties()
                         .Where(x =>
                            x.PropertyType == typeof(string) &&
                            (x.DeclaringType == null || !x.DeclaringType.GetInterfaces()
                                .Any(i => i.IsDefined(typeof(TExcludeAttribute), false))) &&
                            x.GetCustomAttribute<TExcludeAttribute>() == null
                          );
    }

    public ColumnInfo GetColumnInfo<TNameTransformAttribute>(Type entityType, string propertyName, bool useTranslit = true) where TNameTransformAttribute : Attribute
    {
        var propertyInfo = GetCachedProperty(entityType, propertyName);
        if (propertyInfo == null) return null!;

        var hasTranslitAttribute = propertyInfo.GetCustomAttributes(typeof(TNameTransformAttribute), inherit: true).FirstOrDefault() is TNameTransformAttribute;
        var isKey = GetEntityKey(entityType).Equals(propertyName, StringComparison.CurrentCultureIgnoreCase);

        var isTranslit = useTranslit && hasTranslitAttribute;
        var columnName = isTranslit ? UseTranslitColumn(propertyInfo) : propertyInfo.Name;

        var isNullAllowed = IsNullAllowed(propertyInfo);

        return new ColumnInfo(columnName, propertyInfo.PropertyType, isTranslit, isNullAllowed, isKey);
    }

    public string GetEntityKey(Type entityType)
        => EntityKeyCache.GetOrAdd(entityType, static t =>
        {
            var properties = t.GetProperties();

            var keyProperty = properties.SingleOrDefault(p =>
                p.DeclaringType == t &&
                p.GetCustomAttributes(typeof(KeyAttribute), inherit: false).Any());

            if (keyProperty != null) return JsonNamingPolicy.CamelCase.ConvertName(keyProperty.Name);

            keyProperty = properties.SingleOrDefault(p =>
                p.DeclaringType != t &&
                p.GetCustomAttributes(typeof(KeyAttribute), inherit: true).Any());

            if (keyProperty != null) return JsonNamingPolicy.CamelCase.ConvertName(keyProperty.Name);

            throw new MissingEntityKeyException($"Entity type {t.Name} has no key property.");
        });

    public TypeCode GetTypeCodeForProperty(Type entityType, string propertyName)
        => TypeCodeCache.GetOrAdd(
            new TypeNameKey(entityType, propertyName),
            static key =>
            {
                var property = GetCachedProperty(key.Type, key.Name);
                Guard.AgainstNull(property);
                var propertyType = Nullable.GetUnderlyingType(property!.PropertyType) ?? property.PropertyType;
                return Type.GetTypeCode(propertyType);
            });

    public bool IsFieldFilterable(Type entityType, string propertyName)
        => FilterableCache.GetOrAdd(
            new TypeNameKey(entityType, propertyName),
            static key =>
            {
                var prop = GetCachedProperty(key.Type, key.Name);
                return prop != null && prop.GetCustomAttribute<NonFilterableAttribute>() is null;
            });

    public bool IsFieldSortable(Type entityType, string propertyName)
        => SortableCache.GetOrAdd(
            new TypeNameKey(entityType, propertyName),
            static key =>
            {
                var prop = GetCachedProperty(key.Type, key.Name);
                return prop != null && prop.GetCustomAttribute<NonSortableAttribute>() is null;
            });

    public bool IsFieldInMeta(Type entityType, string propertyName)
        => InMetaCache.GetOrAdd(
            new TypeNameKey(entityType, propertyName),
            static key => GetCachedProperty(key.Type, key.Name) != null);

    public bool IsNullAllowed(Type entityType, string propertyName)
        => NullAllowedCache.GetOrAdd(
            new TypeNameKey(entityType, propertyName),
            static key =>
            {
                var prop = GetCachedProperty(key.Type, key.Name);
                return prop != null && prop.GetCustomAttribute<NullsAllowedAttribute>() is not null;
            });

    private static bool IsNullAllowed(PropertyInfo propertyInfo)
        => propertyInfo.GetCustomAttribute<NullsAllowedAttribute>() is not null ||
           Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null;

    private static string UseTranslitColumn(MemberInfo propertyInfo)
    {
        return $"{propertyInfo.Name}_translit";
    }

    private static string GetTypeString(TypeCode typeCode)
    {
        var typeStr = typeCode switch
        {
            TypeCode.Boolean => "bool",
            TypeCode.Byte => "byte",
            TypeCode.Char => "char",
            TypeCode.DateTime => "DateTime",
            TypeCode.Decimal => "decimal",
            TypeCode.Double => "double",
            TypeCode.Int16 => "short",
            TypeCode.Int32 => "int",
            TypeCode.Int64 => "long",
            TypeCode.SByte => "sbyte",
            TypeCode.Single => "float",
            TypeCode.String => "string",
            TypeCode.UInt16 => "ushort",
            TypeCode.UInt32 => "uint",
            TypeCode.UInt64 => "ulong",
            TypeCode.Object => "object",
            TypeCode.DBNull or TypeCode.Empty => "null",
            _ => throw new ArgumentOutOfRangeException()
        };
        return typeStr;
    }
}

/// <summary>
/// Cache key combining a CLR type and a property name compared ordinal-ignore-case,
/// avoiding per-call string allocations from ToLowerInvariant().
/// </summary>
internal readonly struct TypeNameKey(Type type, string name) : IEquatable<TypeNameKey>
{
    public Type Type { get; } = type;
    public string Name { get; } = name;

    public bool Equals(TypeNameKey other)
        => Type == other.Type && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is TypeNameKey k && Equals(k);

    public override int GetHashCode()
        => HashCode.Combine(Type, string.GetHashCode(Name, StringComparison.OrdinalIgnoreCase));
}