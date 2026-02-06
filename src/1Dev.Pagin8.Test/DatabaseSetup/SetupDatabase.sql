-- =============================================
-- SQL Server Test Database Setup for Pagin8
-- =============================================

-- 1. Create Database
USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'Pagin8Test')
BEGIN
    CREATE DATABASE Pagin8Test;
END
GO

USE Pagin8Test;
GO

-- 2. Create Archive Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Archive')
BEGIN
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

    -- Create indexes for better performance
    CREATE INDEX IX_Archive_Status ON Archive(Status);
    CREATE INDEX IX_Archive_RecordDate ON Archive(RecordDate);
    CREATE INDEX IX_Archive_Amount ON Archive(Amount);
    CREATE INDEX IX_Archive_Category ON Archive(Category);
    CREATE INDEX IX_Archive_CustomerName ON Archive(CustomerName);
END
GO

-- 3. Generate Test Data (Adjust the loop for more/fewer records)
DECLARE @Counter INT = 0;
DECLARE @MaxRecords INT = 10000; -- Change to 100000 or 600000 for stress testing

WHILE @Counter < @MaxRecords
BEGIN
    INSERT INTO Archive (Status, RecordDate, Amount, CustomerName, Category)
    VALUES
    (
        -- Random Status
        CASE ABS(CHECKSUM(NEWID())) % 4
            WHEN 0 THEN 'Active'
            WHEN 1 THEN 'Pending'
            WHEN 2 THEN 'Completed'
            ELSE 'Cancelled'
        END,
        -- Random Date in last year
        DATEADD(DAY, -ABS(CHECKSUM(NEWID())) % 365, GETDATE()),
        -- Random Amount between 10 and 10000
        CAST((ABS(CHECKSUM(NEWID())) % 9990 + 10) AS DECIMAL(18,2)),
        -- Random Customer Name
        CONCAT(
            CASE ABS(CHECKSUM(NEWID())) % 8
                WHEN 0 THEN 'John'
                WHEN 1 THEN 'Jane'
                WHEN 2 THEN 'Bob'
                WHEN 3 THEN 'Alice'
                WHEN 4 THEN 'Charlie'
                WHEN 5 THEN 'Diana'
                WHEN 6 THEN 'Eve'
                ELSE 'Frank'
            END,
            ' Smith #',
            ABS(CHECKSUM(NEWID())) % 9999
        ),
        -- Random Category
        CASE ABS(CHECKSUM(NEWID())) % 3
            WHEN 0 THEN 'Standard'
            WHEN 1 THEN 'Premium'
            ELSE 'Enterprise'
        END
    );

    SET @Counter = @Counter + 1;

    -- Show progress every 1000 records
    IF @Counter % 1000 = 0
    BEGIN
        PRINT 'Inserted ' + CAST(@Counter AS VARCHAR(10)) + ' records...';
    END
END
GO

-- 4. Verify Data
SELECT 
    COUNT(*) AS TotalRecords,
    MIN(RecordDate) AS OldestRecord,
    MAX(RecordDate) AS NewestRecord,
    AVG(Amount) AS AverageAmount
FROM Archive;
GO

-- 5. Sample Data by Status
SELECT Status, COUNT(*) AS Count
FROM Archive
GROUP BY Status
ORDER BY Count DESC;
GO

-- 6. Sample Data by Category
SELECT Category, COUNT(*) AS Count
FROM Archive
GROUP BY Category
ORDER BY Count DESC;
GO

-- =============================================
-- Test Queries (Compare with Pagin8 DSL)
-- =============================================

-- Test 1: Simple filter (DSL: status=eq.Active)
SELECT TOP 100 * 
FROM Archive 
WHERE Status = 'Active'
ORDER BY Id ASC;
GO

-- Test 2: Amount filter (DSL: amount=gt.500)
SELECT TOP 100 * 
FROM Archive 
WHERE Amount > 500
ORDER BY Id ASC;
GO

-- Test 3: Contains search (DSL: customerName=cs.John)
SELECT TOP 100 * 
FROM Archive 
WHERE CustomerName LIKE '%John%'
ORDER BY Id ASC;
GO

-- Test 4: Date range (DSL: recordDate=gte.2024-01-01&recordDate=lte.2024-12-31)
SELECT TOP 100 * 
FROM Archive 
WHERE RecordDate >= '2024-01-01' AND RecordDate <= '2024-12-31'
ORDER BY Id ASC;
GO

-- Test 5: Complex filter (DSL: status=eq.Active&amount=gt.100&category=eq.Premium)
SELECT TOP 100 * 
FROM Archive 
WHERE Status = 'Active' 
  AND Amount > 100 
  AND Category = 'Premium'
ORDER BY Id ASC;
GO

-- Test 6: IN operator (DSL: id=in.(1,2,3,4,5))
SELECT * 
FROM Archive 
WHERE Id IN (1,2,3,4,5)
ORDER BY Id ASC;
GO

-- =============================================
-- Performance Comparison Query
-- =============================================

-- Simulate the OLD way (fetch all, filter in memory)
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

SELECT * FROM Archive; -- This would fetch ALL 600k rows

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO

-- Simulate the NEW way (server-side filtering)
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

SELECT TOP 200 * 
FROM Archive 
WHERE Status = 'Active' 
  AND Amount > 100
ORDER BY RecordDate DESC;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO

-- =============================================
-- Cleanup (if needed)
-- =============================================

-- TRUNCATE TABLE Archive;
-- DROP TABLE Archive;
-- DROP DATABASE Pagin8Test;
