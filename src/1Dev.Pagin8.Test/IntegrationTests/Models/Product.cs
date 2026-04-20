using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _1Dev.Pagin8.Test.IntegrationTests.Models;

/// <summary>
/// Test entity representing a product in an e-commerce system
/// Used for testing all Pagin8 operators and features
/// </summary>
[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int Stock { get; set; }

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    public string? Tags { get; set; }

    public bool IsFeatured { get; set; }

    public double Rating { get; set; }
}
