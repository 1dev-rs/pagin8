using System.ComponentModel.DataAnnotations;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

public class TestEntity
{
    [Key]
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public class TestNestedEntity
{
    [Key]
    public int Id { get; set; }
    public TestEntity? TestEntity { get; set; }
}