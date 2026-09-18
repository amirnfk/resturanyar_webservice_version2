/* ============================================================
   Undo barista (باریستا) role — role_id = 6
   Refuses if any Users still have this role.
   ============================================================ */

IF EXISTS (SELECT 1 FROM dbo.Roles WHERE role_id = 6)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE role_id = 6)
    BEGIN
        RAISERROR(N'Cannot delete role_id=6: Users still have role باریستا. Reassign or delete those users first.', 16, 1);
    END
    ELSE
    BEGIN
        DELETE FROM dbo.Roles WHERE role_id = 6;
    END
END;
GO
