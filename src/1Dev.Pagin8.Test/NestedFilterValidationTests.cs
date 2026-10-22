using System.ComponentModel.DataAnnotations;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Validators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace _1Dev.Pagin8.Test;

public class TestNestedParent
{
    [Key]
    public int Id { get; set; }
    public TestNestedChild? Child { get; set; }
    public List<TestNestedChild> Children { get; set; } = [];
}

public class TestNestedChild
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [NonFilterable]
    public string Secret { get; set; } = string.Empty;
}

public class NestedFilterValidationTests : Pagin8TestBase
{
    private readonly TokenContextValidator _validator = new(
        new Pagin8MetadataProvider(new MetadataProvider()),
        new ServiceConfiguration(),
        NullLogger<TokenContextValidator>.Instance);

    private readonly Tokenizer _tokenizer = new();

    [Theory]
    [InlineData("child.with=(name.eq.a)")]
    [InlineData("children.with=(name.eq.a)")]
    [InlineData("children.with=(id.in.(1,2))")]
    [InlineData("children.with=(or(name.eq.a,id.eq.2))")]
    public void ValidInnerFields_Pass(string query)
    {
        _validator.ValidateFilterableTokenFields<TestNestedParent>(_tokenizer.Tokenize(query)).Should().BeTrue();
    }

    [Theory]
    [InlineData("child.with=(missing.eq.a)")]
    [InlineData("children.with=(missing.eq.a)")]
    [InlineData("children.with=(or(name.eq.a,missing.eq.2))")]
    public void UnknownInnerField_IsRejected(string query)
    {
        _validator.ValidateFilterableTokenFields<TestNestedParent>(_tokenizer.Tokenize(query)).Should().BeFalse();
    }

    [Theory]
    [InlineData("child.with=(secret.eq.a)")]
    [InlineData("children.with=(secret.eq.a)")]
    public void NonFilterableInnerField_IsRejected(string query)
    {
        _validator.ValidateFilterableTokenFields<TestNestedParent>(_tokenizer.Tokenize(query)).Should().BeFalse();
    }
}
