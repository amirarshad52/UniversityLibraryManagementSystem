
USE ULMS_DB;

DROP TRIGGER IF EXISTS TR_BookCopies_BeforeUpdate;
DROP TRIGGER IF EXISTS TR_Loans_BeforeInsert;
DROP TRIGGER IF EXISTS TR_Loans_AfterInsert;
DROP TRIGGER IF EXISTS TR_Loans_BeforeUpdate;
DROP TRIGGER IF EXISTS TR_Loans_AfterReturn;

DELIMITER $$

-- 1. Protect BookCopies status
CREATE TRIGGER TR_BookCopies_BeforeUpdate
BEFORE UPDATE ON BookCopies
FOR EACH ROW
BEGIN
    IF NOT (NEW.Status <=> OLD.Status)
       AND COALESCE(@ULMS_INTERNAL_COPY_STATUS, 0) <> 1 THEN

        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT =
            'Copy status can only be changed by loan operations';
    END IF;
END$$

-- 2. Check availability and lock copy
CREATE TRIGGER TR_Loans_BeforeInsert
BEFORE INSERT ON Loans
FOR EACH ROW
BEGIN
    DECLARE copy_status VARCHAR(30);

    SELECT Status INTO copy_status
    FROM BookCopies
    WHERE CopyId = NEW.CopyId
    FOR UPDATE;

    IF copy_status <> 'Available' THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Book copy is not available';
    END IF;

    IF NEW.ReturnDate IS NOT NULL
       OR NEW.ReturnedByStaffId IS NOT NULL THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'New loan must be active';
    END IF;
END$$

-- 3. Mark copy Issued after issue
CREATE TRIGGER TR_Loans_AfterInsert
AFTER INSERT ON Loans
FOR EACH ROW
BEGIN
    DECLARE previous_flag INT DEFAULT
        COALESCE(@ULMS_INTERNAL_COPY_STATUS, 0);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        SET @ULMS_INTERNAL_COPY_STATUS = previous_flag;
        RESIGNAL;
    END;

    SET @ULMS_INTERNAL_COPY_STATUS = 1;

    UPDATE BookCopies
    SET Status = 'Issued'
    WHERE CopyId = NEW.CopyId;

    SET @ULMS_INTERNAL_COPY_STATUS = previous_flag;
END$$

-- 4. Protect loan history
CREATE TRIGGER TR_Loans_BeforeUpdate
BEFORE UPDATE ON Loans
FOR EACH ROW
BEGIN
    IF NOT (NEW.CopyId <=> OLD.CopyId)
       OR NOT (NEW.StudentId <=> OLD.StudentId)
       OR NOT (NEW.IssuedByStaffId <=> OLD.IssuedByStaffId)
       OR NOT (NEW.IssueDate <=> OLD.IssueDate)
       OR NOT (NEW.DueDate <=> OLD.DueDate) THEN

        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Loan history fields cannot be changed';
    END IF;

    IF OLD.ReturnDate IS NOT NULL
       AND (
           NOT (NEW.ReturnDate <=> OLD.ReturnDate)
           OR NOT (NEW.ReturnedByStaffId <=> OLD.ReturnedByStaffId)
       ) THEN

        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Returned loan cannot be reopened or modified';
    END IF;
END$$

-- 5. Mark copy Available after return
CREATE TRIGGER TR_Loans_AfterReturn
AFTER UPDATE ON Loans
FOR EACH ROW
BEGIN
    DECLARE previous_flag INT DEFAULT
        COALESCE(@ULMS_INTERNAL_COPY_STATUS, 0);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        SET @ULMS_INTERNAL_COPY_STATUS = previous_flag;
        RESIGNAL;
    END;

    IF OLD.ReturnDate IS NULL
       AND NEW.ReturnDate IS NOT NULL THEN

        SET @ULMS_INTERNAL_COPY_STATUS = 1;

        UPDATE BookCopies
        SET Status = 'Available'
        WHERE CopyId = OLD.CopyId;

        SET @ULMS_INTERNAL_COPY_STATUS = previous_flag;
    END IF;
END$$

DELIMITER ;
