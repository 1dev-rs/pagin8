using FluentAssertions;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Test;

public class TokenizerComparisonTests : Pagin8TestBase
{
    private readonly Tokenizer _tokenizer = new();

    [Fact]
    public void Should_Parse_ComparisonToken_With_Comment()
    {
        var input = "status=eq.active^Filter for active status";

        var tokens = _tokenizer.Tokenize(input);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<ComparisonToken>().Subject;
        token.Field.Should().Be("status");
        token.Operator.Should().Be(ComparisonOperator.Equals);
        token.Value.Should().Be("active");
        token.IsNegated.Should().BeFalse();
        token.Comment.Should().Be("Filter for active status");
    }

    [Fact]
    public void Should_Parse_ComparisonToken_Without_Comment()
    {
        var input = "status=eq.active";

        var tokens = _tokenizer.Tokenize(input);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<ComparisonToken>().Subject;
        token.Field.Should().Be("status");
        token.Operator.Should().Be(ComparisonOperator.Equals);
        token.Value.Should().Be("active");
        token.IsNegated.Should().BeFalse();
        token.Comment.Should().BeNull();
    }


    [Fact]
    public void Should_Parse_Comparison_With_Dot_In_Value()
    {
        var input = "status=eq.act.ive^Dot in value";

        var tokens = _tokenizer.Tokenize(input);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<ComparisonToken>().Subject;
        token.Value.Should().Be("act.ive");
        token.Comment.Should().Be("Dot in value");
    }
}