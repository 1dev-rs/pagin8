using FluentAssertions;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Test;

public class TokenizerGroupTests
{
    private readonly Tokenizer _tokenizer = new();

    [Fact]
    public void Should_Parse_Nested_And_Or_Groups_With_Comments()
    {
        var input = "and=(activity.eq.0^Active only,or(name.stw.Min^Starts with Min,nameEnglish.stw.Ministry),functionalClassificationId.not.in.(010,011,012))";

        var tokens = _tokenizer.Tokenize(input);

        tokens.Should().HaveCount(1);
        var andGroup = tokens[0].Should().BeOfType<GroupToken>().Subject;
        andGroup.NestingOperator.Should().Be(NestingOperator.And);
        andGroup.Tokens.Should().HaveCount(3);

        var innerOr = andGroup.Tokens[1].Should().BeOfType<GroupToken>().Subject;
        innerOr.NestingOperator.Should().Be(NestingOperator.Or);
        innerOr.Tokens.Should().HaveCount(2);
        var innerToken = innerOr.Tokens[0].Should().BeOfType<ComparisonToken>().Subject;
        innerToken.Comment.Should().Be("Starts with Min");
    }

    [Fact]
    public void Should_Handle_Deeply_Nested_Group_Conditions()
    {
        var input = "and=(or(and(status.eq.active,age.gte.18),country.eq.RS),type.eq.admin)";

        var tokens = _tokenizer.Tokenize(input);
        tokens.Should().HaveCount(1);

        var root = tokens[0].Should().BeOfType<GroupToken>().Subject;
        root.NestingOperator.Should().Be(NestingOperator.And);
        root.Tokens.Should().HaveCount(2);

        var nestedOr = root.Tokens[0].Should().BeOfType<GroupToken>().Subject;
        nestedOr.NestingOperator.Should().Be(NestingOperator.Or);

        var innerAnd = nestedOr.Tokens[0].Should().BeOfType<GroupToken>().Subject;
        innerAnd.NestingOperator.Should().Be(NestingOperator.And);
        innerAnd.Tokens.Should().HaveCount(2);
    }

    [Fact]
    public void Should_Parse_Group_With_Comment()
    {
        var input = "and=(status.eq.active,role.eq.admin)^Top-level group";

        var tokens = _tokenizer.Tokenize(input);
        tokens.Should().HaveCount(1);

        var group = tokens[0].Should().BeOfType<GroupToken>().Subject;
        group.NestingOperator.Should().Be(NestingOperator.And);
        group.Comment.Should().Be("Top-level group");
    }
}