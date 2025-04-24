using System.Collections;
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
                var test = currentDepth + 1;
                nestedProperties = Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(arrayInnerType, test).ToList();
            }
        }
        else if (propertyType.IsClass && propertyType != typeof(string) && currentDepth < MaxSerializationDepth)
        {
            var test = currentDepth + 1;
            nestedProperties = Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(propertyType, test).ToList();
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
        var propertyInfo = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo == null) return null!;

        var hasTranslitAttribute = propertyInfo.GetCustomAttributes(typeof(TNameTransformAttribute), inherit: true).FirstOrDefault() is TNameTransformAttribute;
        var isKey = GetEntityKey(entityType).Equals(propertyName, StringComparison.CurrentCultureIgnoreCase);

        var isTranslit = useTranslit && hasTranslitAttribute;
        var columnName = isTranslit ? UseTranslitColumn(propertyInfo) : propertyInfo.Name;

        var isNullAllowed = IsNullAllowed(propertyInfo);

        return new ColumnInfo(columnName, propertyInfo.PropertyType, isTranslit, isNullAllowed, isKey);
    }

    public string GetEntityKey(Type entityType)
    {
        var properties = entityType.GetProperties();

        var keyProperty = properties.SingleOrDefault(p =>
            p.DeclaringType == entityType &&
            p.GetCustomAttributes(typeof(KeyAttribute), inherit: false).Any());

        if (keyProperty != null) return JsonNamingPolicy.CamelCase.ConvertName(keyProperty.Name);

        keyProperty = properties.SingleOrDefault(p =>
            p.DeclaringType != entityType &&
            p.GetCustomAttributes(typeof(KeyAttribute), inherit: true).Any());

        if (keyProperty != null) return JsonNamingPolicy.CamelCase.ConvertName(keyProperty.Name);

        throw new MissingEntityKeyException($"Entity type {entityType.Name} has no key property.");
    }

    public TypeCode GetTypeCodeForProperty(Type entityType, string propertyName)
    {
        var property = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        Guard.AgainstNull(property);

        var propertyType = Nullable.GetUnderlyingType(property!.PropertyType) ?? property.PropertyType;
        return Type.GetTypeCode(propertyType);
    }

    public bool IsFieldFilterable(Type entityType, string propertyName)
    {
        var propertyInfo = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo == null) return false;

        var columnAttribute = propertyInfo.GetCustomAttribute<NonFilterableAttribute>();
        return columnAttribute is null;
    }

    public bool IsFieldSortable(Type entityType, string propertyName)
    {
        var propertyInfo = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo == null) return false;

        var columnAttribute = propertyInfo.GetCustomAttribute<NonSortableAttribute>();

        return columnAttribute is null;
    }

    public bool IsFieldInMeta(Type entityType, string propertyName)
    {
        var propertyInfo = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        return propertyInfo != null;
    }

    public bool IsNullAllowed(Type entityType, string propertyName)
    {
        var propertyInfo = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo == null) return false;

        var nullsAllowedAttribute = propertyInfo.GetCustomAttribute<NullsAllowedAttribute>();

        return nullsAllowedAttribute is not null;
    }

    private static bool IsNullAllowed(PropertyInfo propertyInfo)
    {
        var nullsAllowedAttribute = propertyInfo.GetCustomAttribute<NullsAllowedAttribute>();
        if (nullsAllowedAttribute is not null)
        {
            return true;
        }

        var type = propertyInfo.PropertyType;
        return Nullable.GetUnderlyingType(type) != null;
    }

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