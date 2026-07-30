-- =============================================================
-- TENANT DATABASE (MySQL per tenant)
-- Each tenant gets an isolated MySQL database with its own schema.
-- The backend only invokes stored procedures, never raw SQL.
-- =============================================================

CREATE TABLE IF NOT EXISTS Bookings (
    Id          INT             AUTO_INCREMENT PRIMARY KEY,
    `Date`      DATETIME        NOT NULL,
    CustomerId  INT             NOT NULL,
    ResourceId  INT             NOT NULL,
    CreatedAt   TIMESTAMP       DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

DELIMITER $$

CREATE PROCEDURE IF NOT EXISTS sp_CreateBooking(
    IN `Date` DATETIME,
    IN CustomerId INT,
    IN ResourceId INT
)
BEGIN
    INSERT INTO Bookings (`Date`, CustomerId, ResourceId)
    VALUES (`Date`, CustomerId, ResourceId);

    SELECT Id, `Date`, CustomerId, ResourceId
    FROM Bookings
    WHERE Id = LAST_INSERT_ID();
END$$

CREATE PROCEDURE IF NOT EXISTS sp_ListBookings()
BEGIN
    SELECT Id, `Date`, CustomerId, ResourceId
    FROM Bookings
    ORDER BY `Date` DESC;
END$$

DELIMITER ;
