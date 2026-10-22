using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IpxListenerService.Data
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        private readonly string? _connectionString;

        public DatabaseContextFactory()
        {
        }

        public DatabaseContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DatabaseContext CreateDbContext(string[] args)
        {
            var connStr = !string.IsNullOrWhiteSpace(_connectionString)
                ? _connectionString
                : Program.GetPostgresConnectionString();

            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(connStr);
            return new DatabaseContext(optionsBuilder.Options);
        }

        public DatabaseContext CreateDbContext()
        {
            return CreateDbContext(Array.Empty<string>());
        }
    }
}
