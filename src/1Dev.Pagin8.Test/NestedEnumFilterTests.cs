using System.ComponentModel.DataAnnotations;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace _1Dev.Pagin8.Test;

public class NestedEnumFilterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PostgreSql_RoleFilterBindsNumericJson(int role)
    {
        using var services = CreateProvider(DatabaseType.PostgreSql);
        var query = $"types.with=(type.incl({role}))&treasuryBranchCode=eq.40700";
        var result = services.GetRequiredService<ISqlQueryBuilder>().BuildSqlQuery<Person>(DateOnlyTestHelper.Build(query));

        result.Builder!.AsSql().Sql.Should().Contain("types @> @p0::jsonb");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be($"[{{\"type\": {role}}}]");
    }

    [Theory]
    [InlineData("incl")]
    [InlineData("excl")]
    public void SqlServer_RoleFilterBindsNumericText(string operation)
    {
        using var services = CreateProvider(DatabaseType.SqlServer);
        var query = $"types.with=(type.{operation}(1))";
        var result = services.GetRequiredService<ISqlQueryBuilder>().BuildSqlQuery<Person>(DateOnlyTestHelper.Build(query));

        result.Builder!.AsSql().Sql.Should().Contain("JSON_VALUE(x.value, '$.type')");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("1");
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("2147483648")]
    public void InvalidEnumNumberReturnsInvalidArrayValue(string value)
    {
        using var services = CreateProvider(DatabaseType.PostgreSql);
        var query = $"types.with=(type.incl({value}))";
        var act = () => services.GetRequiredService<ISqlQueryBuilder>().BuildSqlQuery<Person>(DateOnlyTestHelper.Build(query));

        act.Should().Throw<Pagin8Exception>().Which.Code.Should().Be(Pagin8StatusCode.Pagin8_InvalidArrayValue.Code);
    }

    private static ServiceProvider CreateProvider(DatabaseType database) =>
        new ServiceCollection().AddPagin8(options => options.DatabaseType = database).BuildServiceProvider();

    private class Person
    {
        [Key]
        public int Id { get; set; }
        public string TreasuryBranchCode { get; set; } = string.Empty;
        public IReadOnlyList<RoleAssignment> Types { get; set; } = [];
    }

    private class RoleAssignment
    {
        public Role Type { get; set; }
    }

    private enum Role { Cashier = 1, VaultKeeper, CommissionMember, PaymentOperator }
}
