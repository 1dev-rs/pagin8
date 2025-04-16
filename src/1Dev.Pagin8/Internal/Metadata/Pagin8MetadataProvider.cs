using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Metadata.Models;

namespace _1Dev.Pagin8.Internal.Metadata;
public class Pagin8MetadataProvider(IMetadataProvider metadataProvider) : IPagin8MetadataProvider
{
    #region Public methods
    public IEnumerable<ColumnMetadata> Get<TEntity>() where TEntity : class
    {
        return Get(typeof(TEntity));
    }

    public IEnumerable<ColumnMetadata> Get(Type entityType)
    {
        return metadataProvider.Get<NonFilterableAttribute, NonSortableAttribute, MetaExcludeAttribute>(entityType);
    }

    public ColumnInfo GetColumnInfo<TEntity>(string propertyName, bool useTranslit = true) where TEntity : class
    {
        return GetColumnInfo(typeof(TEntity), propertyName, useTranslit);
    }

    public ColumnInfo GetColumnInfo(Type type, string propertyName, bool useTranslit = true)
    {
        var columnInfo = metadataProvider.GetColumnInfo<UseTranslitAttribute>(type, propertyName, useTranslit);
        return columnInfo;
    }

    public string GetEntityKey<TEntity>() where TEntity : class => metadataProvider.GetEntityKey(typeof(TEntity));

    public string GetEntityKey(Type entityType) => metadataProvider.GetEntityKey(entityType);

    public TypeCode GetTypeCodeForProperty<TEntity>(string propertyName) where TEntity : class => metadataProvider.GetTypeCodeForProperty(typeof(TEntity), propertyName);

    public TypeCode GetTypeCodeForProperty(Type entityType, string propertyName) => metadataProvider.GetTypeCodeForProperty(entityType, propertyName);

    public bool IsFieldFilterable<TEntity>(string propertyName) where TEntity : class => metadataProvider.IsFieldFilterable(typeof(TEntity), propertyName);

    public bool IsFieldSortable<TEntity>(string propertyName) where TEntity : class => metadataProvider.IsFieldSortable(typeof(TEntity), propertyName);
    
    public bool IsFieldInMeta<TEntity>(string propertyName) where TEntity : class => metadataProvider.IsFieldInMeta(typeof(TEntity), propertyName);

    public bool IsNullAllowed<TEntity>(string propertyName) where TEntity : class => metadataProvider.IsNullAllowed(typeof(TEntity), propertyName);

    #endregion
}
