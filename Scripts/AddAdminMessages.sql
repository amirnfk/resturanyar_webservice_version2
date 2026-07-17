-- Admin-to-Restaurant Messaging Tables
-- Run on SQL Server if not using EF migrations

CREATE TABLE AdminMessages (
    Id             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Title          NVARCHAR(200)     NOT NULL,
    Body           NVARCHAR(MAX)     NOT NULL,
    MessageType    TINYINT           NOT NULL,
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_AdminMessages_CreatedAt DEFAULT GETDATE(),
    CreatedByAdmin NVARCHAR(100)     NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_AdminMessages_IsActive DEFAULT 1,
    CONSTRAINT CK_AdminMessages_MessageType CHECK (MessageType IN (0, 1))
);

CREATE TABLE AdminMessageRecipients (
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MessageId    INT NOT NULL,
    RestaurantId INT NOT NULL,
    CONSTRAINT FK_AdminMessageRecipients_Message
        FOREIGN KEY (MessageId) REFERENCES AdminMessages(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AdminMessageRecipients_Restaurant
        FOREIGN KEY (RestaurantId) REFERENCES Restaurants(restaurant_id) ON DELETE CASCADE,
    CONSTRAINT UQ_AdminMessageRecipients_Message_Restaurant UNIQUE (MessageId, RestaurantId)
);

CREATE INDEX IX_AdminMessageRecipients_RestaurantId ON AdminMessageRecipients(RestaurantId);

CREATE TABLE AdminMessageReads (
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MessageId    INT NOT NULL,
    RestaurantId INT NOT NULL,
    ReadAt       DATETIME2 NOT NULL CONSTRAINT DF_AdminMessageReads_ReadAt DEFAULT GETDATE(),
    CONSTRAINT FK_AdminMessageReads_Message
        FOREIGN KEY (MessageId) REFERENCES AdminMessages(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AdminMessageReads_Restaurant
        FOREIGN KEY (RestaurantId) REFERENCES Restaurants(restaurant_id) ON DELETE CASCADE,
    CONSTRAINT UQ_AdminMessageReads_Message_Restaurant UNIQUE (MessageId, RestaurantId)
);

CREATE INDEX IX_AdminMessageReads_RestaurantId ON AdminMessageReads(RestaurantId);
