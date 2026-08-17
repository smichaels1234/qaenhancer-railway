using Microsoft.EntityFrameworkCore;
using backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Get connection string
var connectionString = builder.Configuration.GetConnectionString("QAEnhancerConnection");

// Create a DbContext
var optionsBuilder = new DbContextOptionsBuilder<QAEnhancerDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using (var context = new QAEnhancerDbContext(optionsBuilder.Options))
{
    Console.WriteLine("Clearing Identity tables...");
    
    try
    {
        // Execute SQL to clear Identity tables
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserTokens\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserRoles\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserLogins\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserClaims\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetRoleClaims\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUsers\"");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetRoles\"");
        
        Console.WriteLine("✓ Identity tables cleared successfully!");
        Console.WriteLine("You can now register new users without conflicts.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
