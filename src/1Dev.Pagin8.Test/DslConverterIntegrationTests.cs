using FluentAssertions;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Tokenizer;

namespace _1Dev.Pagin8.Test;

public class DslConverterIntegrationTests : Pagin8TestBase
{
    private readonly Tokenizer _tokenizer = new();

    [Fact]
    public void Should_Convert_CompactDsl_To_FriendlyDsl()
    {
        var compact = "and=(status.eq.active^Only active,or(role.eq.admin^Admins only,role.eq.owner))";

        var expected = """
                       and
                           status eq active ^ Only active
                           or
                               role eq admin ^ Admins only
                               role eq owner
                       """;

        var tokens = _tokenizer.Tokenize(compact);
        var friendly = DslConverter.ToFriendly(tokens);

        friendly.Should().Be(expected.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Should_Convert_Nested_CompactDsl_To_FriendlyDsl()
    {
        var compact = "and=(activity.eq.0^Active ministries,or(name.stw.Ministarstvo^Starts with 'Ministarstvo',nameEnglish.stw.Ministry),functionalClassificationId.not.in.(010,011,012))";

        var expected = """
                       and
                           activity eq 0 ^ Active ministries
                           or
                               name stw Ministarstvo ^ Starts with 'Ministarstvo'
                               nameEnglish stw Ministry
                           functionalClassificationId not in ((010,011,012))
                       """;

        var tokens = _tokenizer.Tokenize(compact);
        var friendly = DslConverter.ToFriendly(tokens);

        friendly.Should().Be(expected.Replace("\r\n", "\n"));
    }
}