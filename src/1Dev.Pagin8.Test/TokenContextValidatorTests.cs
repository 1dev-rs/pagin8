using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace _1Dev.Pagin8.Test;

public class TokenContextValidatorTests : Pagin8TestBase
{
    private readonly Mock<IPagin8MetadataProvider> _mockMetadataProvider;
    private readonly Mock<ILogger<TokenContextValidator>> _mockLogger;
    private readonly TokenContextValidator _validator;
    private readonly Tokenizer _tokenizer;

    public TokenContextValidatorTests()
    {
        _mockMetadataProvider = new Mock<IPagin8MetadataProvider>();
        _mockLogger = new Mock<ILogger<TokenContextValidator>>();
        var config = new ServiceConfiguration();
        _validator = new TokenContextValidator(_mockMetadataProvider.Object, config, _mockLogger.Object);
        _tokenizer = new Tokenizer();

        // Setup: return true for properties that exist in TestEntity, false for non-existent
        _mockMetadataProvider.Setup(m => m.IsFieldFilterable<TestEntity>(It.IsAny<string>()))
            .Returns<string>(fieldName => typeof(TestEntity).GetProperty(fieldName) != null);

        _mockMetadataProvider.Setup(m => m.IsFieldInMeta<TestEntity>(It.IsAny<string>()))
            .Returns<string>(fieldName => typeof(TestEntity).GetProperty(fieldName) != null);
    }

    #region IsToken Tests

    [Fact]
    public void ValidateFilterableTokenFields_IsToken_ValidField_ReturnsTrue()
    {
        // Arrange - "Name" exists in TestEntity
        var tokens = _tokenizer.Tokenize("Name=is.true");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilterableTokenFields_IsToken_InvalidField_ReturnsFalse()
    {
        // Arrange - "InvalidField" does NOT exist in TestEntity
        var tokens = _tokenizer.Tokenize("InvalidField=is.true");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("InvalidField")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateFilterableTokenFields_IsToken_NegatedInvalidField_ReturnsFalse()
    {
        // Arrange - "NonExistentField" does NOT exist in TestEntity
        var tokens = _tokenizer.Tokenize("NonExistentField=is.not.false");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilterableTokenFields_IsToken_WithValidActive_ReturnsTrue()
    {
        // Arrange - "Active" exists in TestEntity
        var tokens = _tokenizer.Tokenize("Active=is.true");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ComparisonToken Tests

    [Fact]
    public void ValidateFilterableTokenFields_ComparisonToken_ValidField_ReturnsTrue()
    {
        // Arrange - "Status" exists in TestEntity
        var tokens = _tokenizer.Tokenize("Status=eq.active");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilterableTokenFields_ComparisonToken_InvalidField_ReturnsFalse()
    {
        // Arrange - "InvalidStatus" does NOT exist in TestEntity
        var tokens = _tokenizer.Tokenize("InvalidStatus=eq.active");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Mixed Token Tests

    [Fact]
    public void ValidateFilterableTokenFields_MultipleTokens_AllValid_ReturnsTrue()
    {
        // Arrange - "Name", "Active", "Status" all exist in TestEntity
        var tokens = _tokenizer.Tokenize("Name=eq.John&Active=is.true&Status=eq.active");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilterableTokenFields_MultipleTokens_SomeInvalid_ReturnsFalse()
    {
        // Arrange - "Name" exists, "InvalidField" does NOT
        var tokens = _tokenizer.Tokenize("Name=is.true&InvalidField=eq.test");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilterableTokenFields_MixedIsAndComparison_SomeInvalid_ReturnsFalse()
    {
        // Arrange - "Active" exists, "FakeField" does NOT
        var tokens = _tokenizer.Tokenize("Active=is.true&FakeField=eq.test");

        // Act
        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SelectToken Tests

    [Fact]
    public void ValidateFilterableTokenFields_SelectToken_ValidFields_ReturnsTrue()
    {
        var tokens = _tokenizer.Tokenize("select=Name,Status");

        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilterableTokenFields_SelectToken_InvalidField_ReturnsFalse()
    {
        var tokens = _tokenizer.Tokenize("select=Name,NonExistentField");

        var result = _validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilterableTokenFields_SelectToken_IgnoreUnknownFields_ReturnsTrue_AndStripsInvalidFields()
    {
        var config = new ServiceConfiguration { IgnoreUnknownSelectFields = true };
        var validator = new TokenContextValidator(_mockMetadataProvider.Object, config, _mockLogger.Object);
        var tokens = _tokenizer.Tokenize("select=Name,NonExistentField,Status");

        var result = validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        result.Should().BeTrue();

        var selectToken = tokens.OfType<SelectToken>().Single();
        selectToken.Fields.Should().BeEquivalentTo(["Name", "Status"]);
        selectToken.Fields.Should().NotContain("NonExistentField");
    }

    [Fact]
    public void ValidateFilterableTokenFields_SelectToken_IgnoreUnknownFields_LogsWarning()
    {
        var config = new ServiceConfiguration { IgnoreUnknownSelectFields = true };
        var mockLogger = new Mock<ILogger<TokenContextValidator>>();
        var validator = new TokenContextValidator(_mockMetadataProvider.Object, config, mockLogger.Object);
        var tokens = _tokenizer.Tokenize("select=Name,RemovedColumn");

        validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RemovedColumn")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateFilterableTokenFields_SelectToken_IgnoreUnknownFields_AllValid_KeepsAllFields()
    {
        var config = new ServiceConfiguration { IgnoreUnknownSelectFields = true };
        var validator = new TokenContextValidator(_mockMetadataProvider.Object, config, _mockLogger.Object);
        var tokens = _tokenizer.Tokenize("select=Name,Status,Active");

        var result = validator.ValidateFilterableTokenFields<TestEntity>(tokens);

        result.Should().BeTrue();

        var selectToken = tokens.OfType<SelectToken>().Single();
        selectToken.Fields.Should().BeEquivalentTo(["Name", "Status", "Active"]);
    }

    #endregion

    // Test entity class - only use properties from this class in tests!
    private class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }
}
