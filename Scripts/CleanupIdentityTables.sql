-- Clean up AspNetUsers table and reset for fresh start
-- Run this script if you're getting duplicate key errors

-- Delete all users (this will cascade to related tables)
DELETE FROM "AspNetUserTokens";
DELETE FROM "AspNetUserRoles";
DELETE FROM "AspNetUserLogins";
DELETE FROM "AspNetUserClaims";
DELETE FROM "AspNetUsers";

-- Verify the Id column is text type (it should be for GUID strings)
-- This query will show you the column type
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'AspNetUsers' AND column_name = 'Id';

-- If you see any sequences that shouldn't exist, drop them
-- (AspNetUsers should use GUIDs, not sequences)
DO $$ 
BEGIN
    IF EXISTS (SELECT 1 FROM pg_sequences WHERE schemaname = 'public' AND sequencename = 'AspNetUsers_Id_seq') THEN
        DROP SEQUENCE public."AspNetUsers_Id_seq" CASCADE;
    END IF;
END $$;
