using Omni.Web.Models;

namespace Omni.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Flight> Flights { get; set; } = default!;
    }
}