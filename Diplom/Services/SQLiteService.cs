using System;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Diplom.Services;

public class SQLiteService
{
    public static void start()
    {
        Log.Information("Starting SQLite");
        
        try
        {
            Log.Information("Connecting SQLite");
            using var connection = new SqliteConnection("Data Source=identifier.sqlite");

            connection.Open();

            Log.Information("Executing SQLite");
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT name
                                  FROM nt
                              """;

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var name = reader.GetString(0);
    
                Log.Information($"{name}");
                Console.WriteLine($"Hello, {name}!");
            }
            }
        catch (Exception e)
        {
            Log.Error(e, "fuck");
            throw;
        }
        
    }
}