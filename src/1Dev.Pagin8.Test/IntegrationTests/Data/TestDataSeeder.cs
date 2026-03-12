using Bogus;
using _1Dev.Pagin8.Test.IntegrationTests.Models;

namespace _1Dev.Pagin8.Test.IntegrationTests.Data;

/// <summary>
/// Generates realistic test data for integration tests using Bogus
/// </summary>
public static class TestDataSeeder
{
    private static readonly string[] Categories = 
    { 
        "Electronics", "Clothing", "Books", "Home & Garden", 
        "Sports", "Toys", "Food & Beverage", "Health & Beauty" 
    };

    private static readonly string[] Statuses = 
    { 
        "Active", "Inactive", "Discontinued", "ComingSoon", "OutOfStock" 
    };

    private static readonly string[] Tags =
    {
        "bestseller", "new-arrival", "on-sale", "eco-friendly",
        "limited-edition", "premium", "budget-friendly", null
    };

    /// <summary>
    /// Generates realistic test products using Bogus faker library
    /// </summary>
    /// <param name="count">Number of products to generate</param>
    /// <param name="seed">Random seed for reproducibility (default: 42)</param>
    public static List<Product> GenerateProducts(int count = 1000, int seed = 42)
    {
        // Set seed for reproducible test data
        Randomizer.Seed = new Random(seed);
        
        var productFaker = new Faker<Product>()
            // Generate realistic product names combining adjectives and product types
            .RuleFor(p => p.Name, f => 
                $"{f.Commerce.ProductAdjective()} {f.Commerce.Product()}")
            
            // Random category from our predefined list
            .RuleFor(p => p.Category, f => f.PickRandom(Categories))
            
            // Random status from our predefined list
            .RuleFor(p => p.Status, f => f.PickRandom(Statuses))
            
            // Price between $10 and $1000, rounded to 2 decimals
            .RuleFor(p => p.Price, f => Math.Round(f.Finance.Amount(10, 1000), 2))
            
            // Stock between 0 and 1000 units
            .RuleFor(p => p.Stock, f => f.Random.Int(0, 1000))
            
            // Realistic company names as brands (50% chance of null)
            .RuleFor(p => p.Brand, f => f.Company.CompanyName().OrNull(f, 0.5f))
            
            // Product descriptions (30% chance of null for variety)
            .RuleFor(p => p.Description, f => 
                f.Commerce.ProductDescription().OrNull(f, 0.3f))
            
            // Created date within last 2 years
            .RuleFor(p => p.CreatedAt, f => f.Date.Past(2))
            
            // Updated date within last 30 days (50% chance of null)
            .RuleFor(p => p.UpdatedAt, f => 
                f.Date.Recent(30).OrNull(f, 0.5f))
            
            // Random tags (allowing null for variety)
            .RuleFor(p => p.Tags, f => f.PickRandom(Tags))
            
            // 20% chance of being featured
            .RuleFor(p => p.IsFeatured, f => f.Random.Bool(0.2f))
            
            // Rating between 1.0 and 5.0, rounded to 1 decimal
            .RuleFor(p => p.Rating, f => Math.Round(f.Random.Double(1.0, 5.0), 1));
        
        return productFaker.Generate(count);
    }

    /// <summary>
    /// SQL script to create Products table for SQL Server
    /// </summary>
    public static string GetSqlServerCreateTableScript()
    {
        return @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
            BEGIN
                CREATE TABLE Products (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    Category NVARCHAR(50) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    Stock INT NOT NULL,
                    Brand NVARCHAR(100),
                    Description NVARCHAR(500),
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME,
                    Tags NVARCHAR(100),
                    IsFeatured BIT NOT NULL,
                    Rating FLOAT NOT NULL
                );

                CREATE INDEX IX_Products_Category ON Products(Category);
                CREATE INDEX IX_Products_Status ON Products(Status);
                CREATE INDEX IX_Products_Price ON Products(Price);
                CREATE INDEX IX_Products_Brand ON Products(Brand);
                CREATE INDEX IX_Products_CreatedAt ON Products(CreatedAt);
                CREATE INDEX IX_Products_IsFeatured ON Products(IsFeatured);
            END";
    }

    /// <summary>
    /// SQL script to create Products table for PostgreSQL
    /// </summary>
    public static string GetPostgreSqlCreateTableScript()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Products (
                Id SERIAL PRIMARY KEY,
                Name VARCHAR(200) NOT NULL,
                Category VARCHAR(50) NOT NULL,
                Status VARCHAR(20) NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                Stock INTEGER NOT NULL,
                Brand VARCHAR(100),
                Description VARCHAR(500),
                CreatedAt TIMESTAMP NOT NULL,
                UpdatedAt TIMESTAMP,
                Tags VARCHAR(100),
                IsFeatured BOOLEAN NOT NULL,
                Rating DOUBLE PRECISION NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Products_Category ON Products(Category);
            CREATE INDEX IF NOT EXISTS IX_Products_Status ON Products(Status);
            CREATE INDEX IF NOT EXISTS IX_Products_Price ON Products(Price);
            CREATE INDEX IF NOT EXISTS IX_Products_Brand ON Products(Brand);
            CREATE INDEX IF NOT EXISTS IX_Products_CreatedAt ON Products(CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_Products_IsFeatured ON Products(IsFeatured);
        ";
    }

    /// <summary>
    /// SQL Server INSERT statement template
    /// </summary>
    public static string GetSqlServerInsertScript()
    {
        return @"
            INSERT INTO Products (Name, Category, Status, Price, Stock, Brand, Description, CreatedAt, UpdatedAt, Tags, IsFeatured, Rating)
            VALUES (@Name, @Category, @Status, @Price, @Stock, @Brand, @Description, @CreatedAt, @UpdatedAt, @Tags, @IsFeatured, @Rating)
        ";
    }

    /// <summary>
    /// PostgreSQL INSERT statement template
    /// </summary>
    public static string GetPostgreSqlInsertScript()
    {
        return GetSqlServerInsertScript(); // Same syntax for insert
    }
}
