

using FluentAssertions;
using _1Dev.Pagin8.Internal;

namespace _1Dev.Pagin8.Test;

public class DslConverterToCompactTests
{
    [Fact]
    public void SingleCondition_WithComment_ShouldConvertCorrectly()
    {
        var input = "activity eq 0 ^ Active ministries";
        var expected = "activity=eq.0^Active ministries";

        var result = DslConverter.ToCompact(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void MultipleConditions_WithAndAndOr_ShouldNestCorrectly()
    {
        var input = """
                    and ^ Filter group for active ministries
                    
                        activity eq 0 ^ Active ministries
                    
                        or ^ Check both Serbian and English names
                            name stw Ministarstvo ^ Starts with 'Ministarstvo'
                            nameEnglish stw Ministry
                    
                        functionalClassificationId not in (010,011,012)
                    """;

        var expected = "and=(activity.eq.0^Active ministries,or(name.stw.Ministarstvo^Starts with 'Ministarstvo',nameEnglish.stw.Ministry),functionalClassificationId.not.in.(010,011,012))^Filter group for active ministries";

        var result = DslConverter.ToCompact(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldIgnoreEmptyLines_AndTrimWhitespace()
    {
        var input = @"
        and

            activity eq 0

            name stw TestName
        ";

        var expected = "and=(activity.eq.0,name.stw.TestName)";

        var result = DslConverter.ToCompact(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldHandleNestedGroupsCorrectly()
    {
        var input = """
                    and
                        or
                            name stw A
                            name stw B
                        age gt 18
                    """;

        var expected = "and=(or(name.stw.A,name.stw.B),age.gt.18)";

        var result = DslConverter.ToCompact(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldHandleNegationCorrectly()
    {
        var input = """
                    and
                        status not eq closed
                        isDeleted eq false
                    """;

        var expected = "and=(status.not.eq.closed,isDeleted.eq.false)";

        var result = DslConverter.ToCompact(input);

        result.Should().Be(expected);
    }
}