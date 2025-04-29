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

        public async Task<IEnumerable<Users>> GetUsers()
        {
            string query = "SELECT DISTINCT [UserName] = username FROM [CardGrabber].[dbo].[UserNames]";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var result = await connection.QueryAsync<Users>(query);
                return result;
            }
        }

        public async Task InsertUser(Users user)
        {
            string query = @"
IF NOT EXISTS (SELECT TOP 1 Username FROM [CardGrabber].[dbo].[UserNames] WHERE Username = @user)
BEGIN
INSERT INTO [CardGrabber].[dbo].[UserNames] (username) VALUES @user
END
";
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var result = await connection.ExecuteAsync(query);

            }
        }

        public async Task WriteData(
            string userName,
            int doubleRareItems, float doubleRarePrice,
            int ultraRareItems, float ultraRarePrice,
            int illustrationRareItems, float illustrationRarePrice,
            Guid runId)
        {
            string query = @"
        INSERT INTO [CardGrabber].[dbo].[ResultsDetailed] (
            runId,
            Username,
            InsertDate,
            DoubleRareItems,
            DoubleRareAvgPrice,
            UltraRareItems,
            UltraRareAvgPrice,
            IllustrationRareItems,
            IllustrationRareAvgPrice
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
            @illustrationRarePrice
        )";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(query, new
                {
                    runId = runId.ToString(),
                    userName,
                    doubleRareItems,
                    doubleRarePrice,
                    ultraRareItems,
                    ultraRarePrice,
                    illustrationRareItems,
                    illustrationRarePrice
                });
            }
        }
    }

    public class Users
    {
        public string Name { get; set; }
    }
}
