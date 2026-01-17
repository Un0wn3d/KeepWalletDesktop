using MySqlConnector;

namespace Diplom.Helpers;

public class DatabaseService
{
    await using var connection = new MySqlConnection("Server=myserver;User ID=mylogin;Password=mypass;Database=mydatabase");
    await connection.OpenAsync();

    await using var command = new MySqlCommand("SELECT field FROM table;", connection);
    await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
    Console.WriteLine(reader.GetString(0));

}