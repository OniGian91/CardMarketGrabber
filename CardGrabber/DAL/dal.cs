using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CardGrabber.DAL
{
    internal class dal
    {
        private readonly string _connectionString = "Data Source=localhost;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

        // Constructor to initialize connection string
        public dal()
        {
        }



        public async Task WriteData(
            string userName,
            int doubleRareItems, float doubleRarePrice,
            int ultraRareItems, float ultraRarePrice,
            int illustrationRareItems, float illustrationRarePrice,
            Guid runId)
        {
            string query = @"
        INSERT INTO [CardGrabber].[dbo].[Results] (
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

    // Example data model class to hold the data from the query
    public class Users
    {
        public string Name { get; set; }
    }
}
