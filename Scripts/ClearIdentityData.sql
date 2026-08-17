-- Script to clear all AspNetUsers data without dropping the database
-- This will fix the duplicate key issue

BEGIN;

-- Delete all Identity-related data
DELETE FROM "AspNetUserTokens";
DELETE FROM "AspNetUserRoles";
DELETE FROM "AspNetUserLogins";
DELETE FROM "AspNetUserClaims";
DELETE FROM "AspNetRoleClaims";
DELETE FROM "AspNetUsers";
DELETE FROM "AspNetRoles";

COMMIT;

-- Verify tables are empty
SELECT 'AspNetUsers' as TableName, COUNT(*) as RecordCount FROM "AspNetUsers"
UNION ALL
SELECT 'AspNetRoles', COUNT(*) FROM "AspNetRoles"
UNION ALL
SELECT 'AspNetUserRoles', COUNT(*) FROM "AspNetUserRoles";
