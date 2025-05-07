using CardGrabber.Classes;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CardGrabber.DAL
{
    internal class dal
    {
        private readonly string _connectionString = "Data Source=localhost;Initial Catalog=CardGrabber;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

        public dal()
        {
        }


        public async Task<Run> CreateNewRun(Guid runIdentifier)
        {
            string sqlQuery = @"

INSERT INTO CardGrabber.dbo.Runs ([RunIdentifier],[Start],[Status], [Type])
VALUES
(@RunIdentifier,@Now, @Status, @type)

SELECT [RunId],[RunIdentifier],[Start],[End] 
FROM 
    CardGrabber.dbo.Runs 
WHERE 
    [RunId] = SCOPE_IDENTITY()
";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var run = await connection.QuerySingleAsync<Run>(sqlQuery, new { RunIdentifier  = runIdentifier, DateTime.Now, Status = "In Progress", type = "Test" });
                return run;
            }
        }

        public async Task CompleteRun(int runId, string status)
        {
            string sqlQuery = @"UPDATE CardGrabber.dbo.Runs SET [End] = @Now, [Status] = @status WHERE RunId = @runId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(sqlQuery, new { runId, DateTime.Now, status });
            }
        }


        public async Task<IEnumerable<Sellers>> GetSellers()
        {
            string query = "SELECT DISTINCT UserName,Type,Info,CardMarketRank,Country FROM [CardGrabber].[dbo].[Sellers]";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var result = await connection.QueryAsync<Sellers>(query);
                return result;
            }
        }

    
        public async Task StoreSellerItemInfo(
            string userName,
            int doubleRareItems, float doubleRarePrice,
            int ultraRareItems, float ultraRarePrice,
            int illustrationRareItems, float illustrationRarePrice,
            int holoRareItems, float holoRareAvgPrice,
            Run run)
        {
            string query = @"
 INSERT INTO [CardGrabber].[dbo].[SellerItemsInfo] (
     runId,
     Username,
     InsertDate,
     DoubleRareItems,
     DoubleRareAvgPrice,
     UltraRareItems,
     UltraRareAvgPrice,
     IllustrationRareItems,
     IllustrationRareAvgPrice,
     holoRareItems,
     holoRareAvgPrice
 )
 VALUES (
     @runId,
     @userName,
     GETDATE(),
     @doubleRareItems,
     @doubleRarePrice,
     @ultraRareItems,
     @ultraRarePrice,
     @illustrationRareItems,
     @illustrationRarePrice,
     @holoRareItems,
     @holoRareAvgPrice
 )";
 
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(query, new
                {
                    runId = run.RunId,
                    userName,
                    doubleRareItems,
                    doubleRarePrice,
                    ultraRareItems,
                    ultraRarePrice,
                    illustrationRareItems,
                    illustrationRarePrice,
                    holoRareItems,
                    holoRareAvgPrice
                });
            }
        }

        public async Task InsertSellerAsync(Sellers seller, Run run)
        {
            const string selectQuery = @"
SELECT TOP 1 *
FROM [CardGrabber].[dbo].[Sellers]
WHERE Username = @Username
ORDER BY InsertDate DESC";

            const string insertQuery = @"
INSERT INTO [CardGrabber].[dbo].[Sellers] (
    runId,
    Username,
    Type,
    Info,
    CardMarketRank,
    Country,
    Singles,
    Buy,
    Sell,
    SellNotSent,
    SellNotArrived,
    BuyNotPayed,
    BuyNotReceived,
    InsertDate
)
VALUES (
    @RunId,
    @Username,
    @Type,
    @Info,
    @CardMarketRank,
    @Country,
    @Singles,
    @Buy,
    @Sell,
    @SellNotSent,
    @SellNotArrived,
    @BuyNotPayed,
    @BuyNotReceived,
    @Now
);";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var existing = await connection.QueryFirstOrDefaultAsync<Sellers>(selectQuery, new { seller.Username });

                if (existing == null || !IsSameSeller(existing, seller))
                {
                    var parameters = new
                    {
                        run.RunId,
                        seller.Username,
                        seller.Type,
                        seller.Info,
                        seller.CardMarketRank,
                        seller.Country,
                        seller.Singles,
                        seller.Buy,
                        seller.Sell,
                        seller.SellNotSent,
                        seller.SellNotArrived,
                        seller.BuyNotPayed,
                        seller.BuyNotReceived,
                        DateTime.Now
                    };

                    await connection.ExecuteAsync(insertQuery, parameters);
                }
            }
        }


        private bool IsSameSeller(Sellers oldSeller, Sellers newSeller)
        {
            return oldSeller.Type == newSeller.Type &&
                   oldSeller.Info == newSeller.Info &&
                   oldSeller.CardMarketRank == newSeller.CardMarketRank &&
                   oldSeller.Country == newSeller.Country &&
                   oldSeller.Singles == newSeller.Singles &&
                   oldSeller.Buy == newSeller.Buy &&
                   oldSeller.Sell == newSeller.Sell &&
                   oldSeller.SellNotSent == newSeller.SellNotSent &&
                   oldSeller.SellNotArrived == newSeller.SellNotArrived &&
                   oldSeller.BuyNotPayed == newSeller.BuyNotPayed &&
                   oldSeller.BuyNotReceived == newSeller.BuyNotReceived;
        }


        public async Task InsertCardInfo(int RunId, int cardId, string info)
        {
            const string query = @"
            INSERT INTO dbo.CardsInfo (RunID, CardID, CollectDate, Info)
            VALUES (@RunId, @CardID, @CollectDate, @Info);";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(query, new
            {
                RunId,
                CardID = cardId,
                CollectDate = DateTime.Now,
                Info = info
            });
        }

        public async Task<List<Card>> GetAllCardsAsync()
        {
            const string query = "SELECT CardID, CardName, CardUrl FROM dbo.Cards";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var cards = await connection.QueryAsync<Card>(query);
            return cards.AsList();
        }

    }

}
