-- =============================================
-- SQL Server Stress Test Database Setup (300k records)
-- =============================================

USE master;
GO

-- Drop existing database if needed (comment out if you want to keep existing data)
-- IF EXISTS (SELECT * FROM sys.databases WHERE name = 'Pagin8Test')
-- BEGIN
--     ALTER DATABASE Pagin8Test SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE Pagin8Test;
-- END
-- GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'Pagin8Test')
BEGIN
    CREATE DATABASE Pagin8Test;
END
GO

USE Pagin8Test;
GO

-- Drop and recreate table for clean start
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Archive')
BEGIN
    DROP TABLE Archive;
END
GO

CREATE TABLE Archive (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Status NVARCHAR(50) NOT NULL,
    RecordDate DATETIME NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CustomerName NVARCHAR(200) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    CreatedDate DATETIME DEFAULT GETDATE(),
    ModifiedDate DATETIME DEFAULT GETDATE()
);
GO

-- Create indexes AFTER bulk insert for better performance
PRINT 'Table created. Starting bulk data insertion...';
GO

-- =============================================
-- OPTIMIZED BULK INSERT FOR 300K RECORDS
-- =============================================
SET NOCOUNT ON;
DECLARE @StartTime DATETIME = GETDATE();
DECLARE @BatchSize INT = 1000;
DECLARE @TotalRecords INT = 300000;
DECLARE @CurrentBatch INT = 0;

PRINT 'Generating ' + CAST(@TotalRecords AS VARCHAR(20)) + ' records in batches of ' + CAST(@BatchSize AS VARCHAR(10)) + '...';

WHILE @CurrentBatch * @BatchSize < @TotalRecords
BEGIN
    INSERT INTO Archive (Status, RecordDate, Amount, CustomerName, Category)
    SELECT TOP (@BatchSize)
        -- Random Status
        CASE ABS(CHECKSUM(NEWID())) % 4
            WHEN 0 THEN 'Active'
            WHEN 1 THEN 'Pending'
            WHEN 2 THEN 'Completed'
            ELSE 'Cancelled'
        END,
        -- Random Date in last 2 years (more variety)
        DATEADD(DAY, -ABS(CHECKSUM(NEWID())) % 730, GETDATE()),
        -- Random Amount between 10 and 1000
        CAST((ABS(CHECKSUM(NEWID())) % 990 + 10) AS DECIMAL(18,2)) + 
        CAST((ABS(CHECKSUM(NEWID())) % 100) AS DECIMAL(18,2)) / 100.0,
        -- Random Customer Name with more variety
        CONCAT(
            CASE ABS(CHECKSUM(NEWID())) % 10
                WHEN 0 THEN 'John'
                WHEN 1 THEN 'Jane'
                WHEN 2 THEN 'Bob'
                WHEN 3 THEN 'Alice'
                WHEN 4 THEN 'Charlie'
                WHEN 5 THEN 'Diana'
                WHEN 6 THEN 'Eve'
                WHEN 7 THEN 'Frank'
                WHEN 8 THEN 'Grace'
                ELSE 'Henry'
            END,
            ' ',
            CASE ABS(CHECKSUM(NEWID())) % 10
                WHEN 0 THEN 'Smith'
                WHEN 1 THEN 'Johnson'
                WHEN 2 THEN 'Williams'
                WHEN 3 THEN 'Brown'
                WHEN 4 THEN 'Jones'
                WHEN 5 THEN 'Garcia'
                WHEN 6 THEN 'Miller'
                WHEN 7 THEN 'Davis'
                WHEN 8 THEN 'Rodriguez'
                ELSE 'Martinez'
            END,
            ' #',
            RIGHT('00000' + CAST((@CurrentBatch * @BatchSize + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) AS VARCHAR(10)), 6)
        ),
        -- Random Category
        CASE ABS(CHECKSUM(NEWID())) % 3
            WHEN 0 THEN 'Standard'
            WHEN 1 THEN 'Premium'
            ELSE 'Enterprise'
        END
    FROM 
        -- Cross join to generate enough rows per batch
        (SELECT TOP (@BatchSize / 10) 1 AS N FROM sys.objects) AS T1
        CROSS JOIN (SELECT TOP 10 1 AS N FROM sys.objects) AS T2;

    SET @CurrentBatch = @CurrentBatch + 1;

    -- Progress indicator
    IF @CurrentBatch % 10 = 0
    BEGIN
        DECLARE @Progress INT = (@CurrentBatch * @BatchSize);
        DECLARE @PercentComplete DECIMAL(5,2) = (@Progress * 100.0) / @TotalRecords;
        DECLARE @ElapsedSeconds INT = DATEDIFF(SECOND, @StartTime, GETDATE());
        PRINT CONCAT('Progress: ', @Progress, ' / ', @TotalRecords, 
                     ' (', CAST(@PercentComplete AS VARCHAR(10)), '%) - ',
                     'Elapsed: ', @ElapsedSeconds, 's');
    END
END
GO

DECLARE @InsertEnd DATETIME = GETDATE();
PRINT '';
PRINT 'Data insertion complete!';
GO

-- =============================================
-- CREATE INDEXES FOR PERFORMANCE
-- =============================================
PRINT 'Creating indexes...';
GO

CREATE INDEX IX_Archive_Status ON Archive(Status);
CREATE INDEX IX_Archive_RecordDate ON Archive(RecordDate);
CREATE INDEX IX_Archive_Amount ON Archive(Amount);
CREATE INDEX IX_Archive_Category ON Archive(Category);
CREATE INDEX IX_Archive_CustomerName ON Archive(CustomerName);

-- Composite indexes for common query patterns
CREATE INDEX IX_Archive_Status_Category ON Archive(Status, Category);
CREATE INDEX IX_Archive_RecordDate_Amount ON Archive(RecordDate, Amount);
CREATE INDEX IX_Archive_Category_Amount ON Archive(Category, Amount);

PRINT 'Indexes created successfully!';
GO

-- =============================================
-- VERIFY DATA & STATISTICS
-- =============================================
PRINT '';
PRINT '=============================================';
PRINT '  DATABASE SETUP COMPLETE';
PRINT '=============================================';
PRINT '';

SELECT 
    COUNT(*) AS TotalRecords,
    MIN(RecordDate) AS OldestRecord,
    MAX(RecordDate) AS NewestRecord,
    MIN(Amount) AS MinAmount,
    MAX(Amount) AS MaxAmount,
    AVG(Amount) AS AverageAmount
FROM Archive;

PRINT '';
PRINT 'Records by Status:';
SELECT Status, COUNT(*) AS Count, AVG(Amount) AS AvgAmount
FROM Archive
GROUP BY Status
ORDER BY Status;

PRINT '';
PRINT 'Records by Category:';
SELECT Category, COUNT(*) AS Count, AVG(Amount) AS AvgAmount
FROM Archive
GROUP BY Category
ORDER BY Category;

PRINT '';
PRINT 'Records by Month (Last 12 months):';
SELECT 
    FORMAT(RecordDate, 'yyyy-MM') AS YearMonth,
    COUNT(*) AS Count
FROM Archive
WHERE RecordDate >= DATEADD(MONTH, -12, GETDATE())
GROUP BY FORMAT(RecordDate, 'yyyy-MM')
ORDER BY YearMonth DESC;

PRINT '';
PRINT 'Database is ready for stress testing!';
GO
