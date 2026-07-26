using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;

using KeepWalletDesktop.Models;

namespace KeepWalletDesktop.Services;

public class AppDBContext : DbContext
{
    public DbSet<Transaction> Transaction => Set<Transaction>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    public string DbPath { get; }

    public AppDBContext()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "KeepWallet");
        var oldAppFolder = Path.Combine(localAppData, "KeepWalletDesktop");

        Directory.CreateDirectory(appFolder);
        DbPath = Path.Combine(appFolder, "base.db");
        var oldDbPath = Path.Combine(oldAppFolder, "base.db");
        if (!File.Exists(DbPath) && File.Exists(oldDbPath))
        {
            File.Copy(oldDbPath, DbPath);
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public static class SQLiteDbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDBContext();
        db.Database.EnsureCreated();

        // Ensure the savings table exists for users that already had a DB file.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS SavingsGoals (
                ID INTEGER NOT NULL CONSTRAINT PK_SavingsGoals PRIMARY KEY AUTOINCREMENT,
                GoalAmount REAL NOT NULL,
                SavedAmount REAL NOT NULL
            );");

        if (!db.SavingsGoals.Any())
        {
            db.SavingsGoals.Add(new SavingsGoal { GoalAmount = 10000, SavedAmount = 0 });
        }

        if (db.Transaction.Any())
        {
            db.SaveChanges();
            return;
        }

        for (var i = 0; i < 10; i++)
        {
            db.Transaction.Add(new Transaction { Type = "Expense", Cost = 1002 });
            db.Transaction.Add(new Transaction { Type = "Income", Cost = 2000 });
        }

        db.SaveChanges();
    }
}
