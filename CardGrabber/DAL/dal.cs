using CardGrabber.Classes;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CardGrabber.DAL
{
    internal class dal
    {
        private readonly string _connectionString = "Data Source=localhost;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

        public dal()
        {
        }


        public async Task<Run> CreateNewRun(Guid runIdentifier)
        {
            string sqlQuery = @"

INSERT INTO CardGrabber.dbo.Runs ([RunIdentifier],[Start])
VALUES
(@RunIdentifier,@Now)

SELECT [RunId],[RunIdentifier],[Start],[End] 
FROM 
    CardGrabber.dbo.Runs 
WHERE 
    [RunId] = SCOPE_IDENTITY()
";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var run = await connection.QuerySingleAsync<Run>(sqlQuery, new { RunIdentifier  = runIdentifier, DateTime.Now });
                return run;
            }
        }

        public async Task CompleteRun(Run run)
        {
            string sqlQuery = @"UPDATE CardGrabber.dbo.Runs SET End = @now WHERE RunId = @runId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(sqlQuery, new { run.RunId,DateTime.Now });
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

        public async Task InsertSellerAsync(Sellers seller)
        {
            const string selectQuery = @"
SELECT TOP 1 *
FROM [CardGrabber].[dbo].[Sellers]
WHERE Username = @Username
ORDER BY InsertDate DESC";

            const string insertQuery = @"
INSERT INTO [CardGrabber].[dbo].[Sellers] (
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
    GETDATE()
);";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var existing = await connection.QueryFirstOrDefaultAsync<Sellers>(selectQuery, new { seller.Username });

                if (existing == null || !IsSameSeller(existing, seller))
                {
                    await connection.ExecuteAsync(insertQuery, seller);
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





    }

}
