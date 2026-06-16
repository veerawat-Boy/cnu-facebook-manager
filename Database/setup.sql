-- ================================================================
-- CNU Facebook API - Database Setup Script
-- รัน script นี้ใน SQL Server Management Studio หรือ sqlcmd
-- ================================================================

-- สร้าง Database (ถ้ายังไม่มี)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'CnuFacebookDB')
BEGIN
    CREATE DATABASE CnuFacebookDB;
END
GO

USE CnuFacebookDB;
GO

-- ================================================================
-- ตาราง AccessTokenFacebook
-- เก็บ Page Access Token และ Long-Lived Token ของแต่ละ Facebook Page
-- ================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccessTokenFacebook')
BEGIN
    CREATE TABLE AccessTokenFacebook (
        ID              BIGINT          IDENTITY(1,1)   NOT NULL,
        AccessToken     NVARCHAR(512)                   NOT NULL,
        LongLivedToken  NVARCHAR(512)                       NULL,
        PageID          NVARCHAR(50)                    NOT NULL,
        PageName        NVARCHAR(255)                       NULL,
        OpenStatus      NVARCHAR(1)                         NULL    DEFAULT '1',
        CreateUserID    NVARCHAR(100)                   NOT NULL,
        CreateDate      DATE                                NULL,
        CreateTime      VARCHAR(8)                          NULL,
        UpdateUserID    NVARCHAR(100)                       NULL,
        UpdateDate      DATE                                NULL,
        UpdateTime      VARCHAR(8)                          NULL,

        CONSTRAINT PK_AccessTokenFacebook PRIMARY KEY (ID)
    );

    CREATE INDEX IX_AccessTokenFacebook_PageID
        ON AccessTokenFacebook (PageID);

    CREATE INDEX IX_AccessTokenFacebook_CreateUserID
        ON AccessTokenFacebook (CreateUserID);

    PRINT 'Created table AccessTokenFacebook';
END
ELSE
BEGIN
    PRINT 'Table AccessTokenFacebook already exists';
END
GO

PRINT 'Database setup complete.';
GO
