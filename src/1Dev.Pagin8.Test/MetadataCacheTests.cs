using System.Collections;
using System.Reflection;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

public class MetadataCacheTests
{
    [Fact]
    public void UnknownFieldNames_AreNotCached()
    {
        var provider = new MetadataProvider();
        var before = CachedEntries();

        for (var i = 0; i < 500; i++)
        {
            var name = $"unknown_{Guid.NewGuid():N}";
            provider.IsFieldFilterable(typeof(TestEntity), name).Should().BeFalse();
            provider.IsFieldSortable(typeof(TestEntity), name).Should().BeFalse();
            provider.IsFieldInMeta(typeof(TestEntity), name).Should().BeFalse();
            provider.IsNullAllowed(typeof(TestEntity), name).Should().BeFalse();
        }

        // Other tests may add known names concurrently, but never 500 unknown ones
        (CachedEntries() - before).Should().BeLessThan(100);
    }

    [Fact]
    public void KnownFieldNames_StillResolve()
    {
        var provider = new MetadataProvider();

        provider.IsFieldFilterable(typeof(TestEntity), "name").Should().BeTrue();
        provider.IsFieldSortable(typeof(TestEntity), "NAME").Should().BeTrue();
    }

    private static int CachedEntries()
    {
        var fields = typeof(MetadataProvider).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.Name.EndsWith("Cache") && f.FieldType.Name.StartsWith("ConcurrentDictionary"));
        return fields.Sum(f => ((ICollection)f.GetValue(null)!).Count);
    }
}
