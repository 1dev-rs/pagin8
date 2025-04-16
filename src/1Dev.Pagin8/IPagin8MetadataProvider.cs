using _1Dev.Pagin8.Internal.Metadata.Models;

namespace _1Dev.Pagin8;
public interface IPagin8MetadataProvider
{
    public IEnumerable<ColumnMetadata> Get<TEntity>() where TEntity : class;

    public IEnumerable<ColumnMetadata> Get(Type entityType);

    public ColumnInfo GetColumnInfo<TEntity>(string propertyName, bool useTranslit = true) where TEntity : class;

    public ColumnInfo GetColumnInfo(Type type, string propertyName, bool useTranslit = true);

    public string GetEntityKey<TEntity>() where TEntity : class;

    public string GetEntityKey(Type entityType);

    public TypeCode GetTypeCodeForProperty<TEntity>(string propertyName) where TEntity : class;

    public TypeCode GetTypeCodeForProperty(Type entityType, string propertyName);

    public bool IsFieldFilterable<TEntity>(string propertyName) where TEntity : class;

    public bool IsFieldSortable<TEntity>(string propertyName) where TEntity : class;
    
    public bool IsFieldInMeta<T>(string field) where T : class;

    public bool IsNullAllowed<T>(string field) where T : class;
}
