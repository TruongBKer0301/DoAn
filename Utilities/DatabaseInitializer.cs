using LapTopBD.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Utilities
{
    public static class DatabaseInitializer
    {
        public static async Task EnsureContactRequestsTableAsync(ApplicationDbContext context, ILogger logger)
        {
            const string existsSql = "SELECT OBJECT_ID(N'[dbo].[ContactRequests]', N'U')";

            await context.Database.OpenConnectionAsync();
            try
            {
                await using var existsCommand = context.Database.GetDbConnection().CreateCommand();
                existsCommand.CommandText = existsSql;

                var tableObjectId = await existsCommand.ExecuteScalarAsync();
                if (tableObjectId != null && tableObjectId != DBNull.Value)
                {
                    return;
                }

                const string createSql = @"
CREATE TABLE [dbo].[ContactRequests](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[Email] [nvarchar](150) NOT NULL,
	[PhoneNumber] [nvarchar](20) NOT NULL,
	[Message] [nvarchar](2000) NOT NULL,
	[IsRead] [bit] NOT NULL CONSTRAINT [DF_ContactRequests_IsRead] DEFAULT ((0)),
	[ReadAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)
);
";

                await using var createCommand = context.Database.GetDbConnection().CreateCommand();
                createCommand.CommandText = createSql;
                await createCommand.ExecuteNonQueryAsync();

                logger.LogInformation("Created missing ContactRequests table.");
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}