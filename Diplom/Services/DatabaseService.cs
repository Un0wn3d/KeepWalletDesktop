using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Diplom;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        
        string server = config["DataBase:Server"];
        string port = config["DataBase:Port"];
        string dbName = config["DataBase:Name"];
        string user = config["DataBase:User"];
        string password = config["DataBase:Password"];
        
        _connectionString = $"Server={server};Port={port};Database={dbName};User ID={user};Password={password};";
    }

    // Асинхронный метод для чтения данных
    public async Task ReadDataAsync()
    {
        Log.Information("Start DB");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand("SELECT * FROM logs;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Log.Information("SQL query executed, reading rows...");

        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            Log.Information("EventNameShow: {EventNameShow}", id);
        }
        Log.Information("Rows read: Count");

    }

    public async Task<List<Transaction>> GetLatestExpensesAsync(int count = 5)
    {

        Log.Information("GetLatestExpensesAsync Start");
        var result = new List<Transaction>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Вариант 1: Если колонка называется 'id' (нижний регистр)
            await using var command = new MySqlCommand(
                @"SELECT TransactionType, Amount 
              FROM transactions;", connection);

            command.Parameters.AddWithValue("@count", count);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new Transaction
                {
                    TransactionType = reader.GetString("TransactionType"),
                    Amount = reader.GetDecimal("Amount")
                });
            }
            Log.Information("GetLatestExpensesAsync Finish. Count: {Count}", result.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in GetLatestExpensesAsync");
        }

        return result;
    }

    public class Transaction
    {
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
    }
}