/* ============================================================
   Barista (باریستا) staff role — role_id = 6
   Safe for production — additive only; no Users column changes.
   Run in SSMS / Azure Data Studio against your live DB.
   ============================================================ */

-- Roles.role_id is NOT an IDENTITY column — insert the key directly.
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE role_id = 6)
BEGIN
    INSERT INTO dbo.Roles (role_id, role_name)
    VALUES (6, N'باریستا');
END;
GO
