using IpxListenerService.Models;
using Microsoft.EntityFrameworkCore;

namespace IpxListenerService.Data
{
    public class DatabaseContext : DbContext
    {
        private readonly string _connectionString;

        public DbSet<EventModel> Events { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DatabaseContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrWhiteSpace(_connectionString))
            {
                optionsBuilder.UseNpgsql(_connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EventModel>().ToTable("ipx_events");
            modelBuilder.Entity<EventModel>().HasKey(e => e.Id);
            modelBuilder.Entity<EventModel>().Property(e => e.Id).ValueGeneratedOnAdd();
        }
    }
}
