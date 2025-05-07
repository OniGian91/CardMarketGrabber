using CardGrabber.Configuration;
using CardGrabber.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CardGrabber.Services.Internal
{

    internal class RunManager
    {
        private readonly string _connectionString;
        public RunManager()
        {
            var config = ConfigurationLoader.Load();
            _connectionString = config.Database.ConnectionString;
        }

        public async Task<Run> StartRun()
        {
            Guid runId = Guid.NewGuid();
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
            Run run = new Run();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                run = await connection.QuerySingleAsync<Run>(sqlQuery, new { RunIdentifier = runId, DateTime.Now, Status = "In Progress", type = "Test" });
                
            }
            return run;
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

    }
}
