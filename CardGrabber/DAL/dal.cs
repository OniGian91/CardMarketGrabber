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

        // Method to query the database
        public async Task<IEnumerable<Users>> GetDataAsync()
        {
            // Query to retrieve data (replace with your own query)
            string query = "SELECT [userId],[name] FROM [CardGrabber].[dbo].[Users]";

            // Using the connection and Dapper to execute the query
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Execute the query and return the results as an IEnumerable
                var result = await connection.QueryAsync<Users>(query);

                return result;
            }
        }

        public async void WriteData(int userId,int items)
        {
            // Query to retrieve data (replace with your own query)
            string query = "INSERT INTO [CardGrabber].[dbo].[Results] VALUES (@userId,GETDATE(),@items)";

            // Using the connection and Dapper to execute the query
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Execute the query and return the results as an IEnumerable
                var result = await connection.ExecuteAsync(query, new { userId, items });

            }
        }
    }

    // Example data model class to hold the data from the query
    public class Users
    {
        public int userId { get; set; }
        public string Name { get; set; }
    }
}
