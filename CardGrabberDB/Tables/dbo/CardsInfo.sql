CREATE TABLE dbo.CardsInfo
(
    CardID INT NOT NULL, 
    CollectDate DATETIME,
    Info VARCHAR(MAX)

    CONSTRAINT FK_CardsInfo_Cards 
        FOREIGN KEY (CardID) 
        REFERENCES dbo.Cards(CardID)
        ON DELETE CASCADE 
);
