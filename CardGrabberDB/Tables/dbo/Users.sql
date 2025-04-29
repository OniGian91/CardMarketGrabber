create table dbo.Users(
  userId int identity,
  [name] varchar(100),
  CONSTRAINT PK_Users_userId PRIMARY KEY (userId)
)