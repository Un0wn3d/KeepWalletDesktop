// using Microsoft.Extensions.Configuration;
// using MySqlConnector;
// using Serilog;
// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
//
// namespace KeepWalletDesktop.Services;
//
// public class DatabaseService
// {
//     private readonly string _connectionString;
//
//     public DatabaseService()
//     {
//         var config = new ConfigurationBuilder()
//             .SetBasePath(AppContext.BaseDirectory)
//             .AddJsonFile("appsettings.json", optional: false)
//             .Build();
//         
//         string server = config["DataBase:Server"];
//         string port = config["DataBase:Port"];
//         string dbName = config["DataBase:Name"];
//         string user = config["DataBase:User"];
//         string password = config["DataBase:Password"];
//         
//         _connectionString = $"Server={server};Port={port};Database={dbName};User ID={user};Password={password};";
//     }
//
//     // Асинхронный метод для чтения данных
//     public async Task ReadDataAsync()
//     {
//         await using var connection = new MySqlConnection(_connectionString);
//         await connection.OpenAsync();
//
//         await using var command = new MySqlCommand("SELECT * FROM logs;", connection);
//         await using var reader = await command.ExecuteReaderAsync();
//     }
//
//     public async Task<List<Transaction>> GetLatestExpensesAsync(int count = 5)
//     {
//         var result = new List<Transaction>();
//
//         try
//         {
//             await using var connection = new MySqlConnection(_connectionString);
//             await connection.OpenAsync();
//
//             // Вариант 1: Если колонка называется 'id' (нижний регистр)
//             await using var command = new MySqlCommand(
//                 @"SELECT TransactionType, Amount 
//               FROM transactions;", connection);
//
//             command.Parameters.AddWithValue("@count", count);
//
//             await using var reader = await command.ExecuteReaderAsync();
//
//             while (await reader.ReadAsync())
//             {
//                 result.Add(new Transaction
//                 {
//                     TransactionType = reader.GetString("TransactionType"),
//                     Amount = reader.GetDecimal("Amount")
//                 });
//             }
//         }
//         catch (Exception ex)
//         {
//             Log.Error(ex, "Error in GetLatestExpensesAsync");
//         }
//
//         return result;
//     }
//
//     public class Transaction
//     {
//         public string TransactionType { get; set; }
//         public decimal Amount { get; set; }
//     }
// }
