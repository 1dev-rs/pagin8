using FluentAssertions;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Test;

public class DslConverterToFriendlyTests : Pagin8TestBase
{
    [Fact]
    public void Should_Render_Simple_ComparisonToken_With_Comment()
    {
        var tokens = new List<Token>
        {
            new ComparisonToken("status", ComparisonOperator.Equals, "active", 1, false,"Filter for active status")
        };

        var result = DslConverter.ToFriendly(tokens);

        result.Should().Be("status eq active ^ Filter for active status");
    }

    [Fact]
    public void Should_Render_Group_With_Nested_Comparisons_And_Comments()
    {
        var tokens = new List<Token>
        {
            new GroupToken(NestingOperator.And, [
                new ComparisonToken("status", ComparisonOperator.Equals, "active", 2, false, "Only active"),
                new GroupToken(NestingOperator.Or, [
                    new ComparisonToken("role", ComparisonOperator.Equals, "admin", 3, false, "Admins only"),
                    new ComparisonToken("role", ComparisonOperator.Equals, "owner", 3)
                ], 1)
            ], 1, comment:"User eligibility")
        };

        var expected =
            "and ^ User eligibility\n" +
            "    status eq active ^ Only active\n" +
            "    or\n" +
            "        role eq admin ^ Admins only\n" +
            "        role eq owner";

        var result = DslConverter.ToFriendly(tokens);

        result.Should().Be(expected);
    }

    [Fact]
    public void Should_Render_DateRange_With_Exact_And_Comment()
    {
        var tokens = new List<Token>
        {
            new DateRangeToken("created", DateRangeOperator.Ago, 2, DateRange.Week, true, false, 1, false, "Last 2 weeks")
        };

        var result = DslConverter.ToFriendly(tokens);

        result.Should().Be("created ago 2we ^ L" +
                           "ast 2 weeks");
    }

    [Fact]
    public void Should_Render_InToken_With_Comparison_And_Comment()
    {
        var tokens = new List<Token>
        {
            new InToken("tags", "A,B", 1, comment : "Selected tags")
        };

        var result = DslConverter.ToFriendly(tokens);

        result.Should().Be("tags in (A,B) ^ Selected tags");
    }

    [Fact]
    public void Should_Render_IsToken_Negated()
    {
        var tokens = new List<Token>
        {
            new IsToken("active", "true", 1, true, false,  "Must not be active")
        };

        var result = DslConverter.ToFriendly(tokens);

        result.Should().Be("active not is true ^ Must not be active");
    }
}