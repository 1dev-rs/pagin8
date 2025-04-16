
using System.Reflection;
using _1Dev.Pagin8.Internal.Metadata.Models;

public interface IMetadataProvider
{
    public IEnumerable<ColumnMetadata> Get<TNonFilterableAttribute, TNonSortable, TMetaExclude>(Type entityType, int currentDepth = 0) where TNonFilterableAttribute : Attribute where TNonSortable : Attribute where TMetaExclude : Attribute;

    public IEnumerable<PropertyInfo> GetStringProperties<TExcludeAttribute>(Type entityType) where TExcludeAttribute : Attribute;

    public ColumnInfo GetColumnInfo<TNameTransformAttribute>(Type entityType, string propertyName, bool useTranslit = true) where TNameTransformAttribute : Attribute;

    public string GetEntityKey(Type entityType);

    public TypeCode GetTypeCodeForProperty(Type entityType, string propertyName);

    public bool IsFieldFilterable(Type entityType, string propertyName);

    public bool IsFieldSortable(Type entityType, string propertyName);
    
    public bool IsFieldInMeta(Type type, string propertyName);
    
    public bool IsNullAllowed(Type type, string propertyName);
}
