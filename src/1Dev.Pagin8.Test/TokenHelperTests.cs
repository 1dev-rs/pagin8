using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Helpers;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

public class TokenHelperTests
{
    [Fact]
    public void SplitAtDelimiters_ShouldThrowException_WhenParenthesesAreUnbalanced_ClosingWithoutOpening()
    {
        // Arrange
        var input = "field=eq.value)";
        var delimiter = '&';

        // Act
        Action act = () => TokenHelper.SplitAtDelimiters(input, delimiter).ToList();

        // Assert
        act.Should().Throw<Pagin8Exception>()
            .WithMessage("Pagin8_MalformedQuery");
    }

    [Fact]
    public void SplitAtDelimiters_ShouldHandleBalancedParentheses()
    {
        // Arrange
        var input = "field=in.(1,2,3)&other=eq.test";
        var delimiter = '&';

        // Act
        var result = TokenHelper.SplitAtDelimiters(input, delimiter).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("field=in.(1,2,3)");
        result[1].Should().Be("other=eq.test");
    }

    [Fact]
    public void SplitAtDelimiters_ShouldNotSplitWithinParentheses()
    {
        // Arrange
        var input = "or=(field1.eq.1,field2.in.(a,b,c))";
        var delimiter = '&';

        // Act
        var result = TokenHelper.SplitAtDelimiters(input, delimiter).ToList();

        // Assert - Should not split because no delimiter exists outside parentheses
        result.Should().HaveCount(1);
        result[0].Should().Be("or=(field1.eq.1,field2.in.(a,b,c))");
    }

    [Fact]
    public void NormalizeValue_ShouldReturnEmpty_WhenInputIsEmptyParentheses()
    {
        // Arrange
        var input = "()";

        // Act
        var result = TokenHelper.NormalizeValue(input);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeValue_ShouldRemoveOuterParentheses_WhenPresent()
    {
        // Arrange
        var input = "(test value)";

        // Act
        var result = TokenHelper.NormalizeValue(input);

        // Assert
        result.Should().Be("test value");
    }

    [Fact]
    public void NormalizeValue_ShouldReturnOriginal_WhenNoParentheses()
    {
        // Arrange
        var input = "test value";

        // Act
        var result = TokenHelper.NormalizeValue(input);

        // Assert
        result.Should().Be("test value");
    }

    [Fact]
    public void NormalizeValue_ShouldReturnOriginal_WhenOnlyOpeningParenthesis()
    {
        // Arrange
        var input = "(test";

        // Act
        var result = TokenHelper.NormalizeValue(input);

        // Assert
        result.Should().Be("(test");
    }

    [Fact]
    public void NormalizeValue_ShouldHandleNull()
    {
        // Arrange
        string? input = null;

        // Act
        var result = TokenHelper.NormalizeValue(input!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeValue_ShouldHandleWhitespace()
    {
        // Arrange
        var input = "   ";

        // Act
        var result = TokenHelper.NormalizeValue(input);

        // Assert
        result.Should().Be("   ");
    }
}
