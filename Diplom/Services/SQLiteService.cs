using Microsoft.EntityFrameworkCore;

namespace Diplom.Services;

public class AppDBContext : DbContext
{
    public DbSet<Transaction> Transaction { get; set; }

    public string DbPath { get; }

    public AppDBContext()
    {
        // var folder = Environment.SpecialFolder.LocalApplicationData;
        // var path = Environment.GetFolderPath(folder);
        // DbPath = System.IO.Path.Join(path, "base.db");
        DbPath = "base.db";
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public class Transaction
{
    public int ID { get; set; }
    public string Type { get; set; }
    public double Cost { get; set; }
}