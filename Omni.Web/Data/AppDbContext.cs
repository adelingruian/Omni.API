using Omni.Web.Models;

namespace Omni.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Flight> Flights { get; set; } = default!;
        public DbSet<Disruption> Disruptions { get; set; } = default!;
        public DbSet<ResourceUsage> ResourceUsages { get; set; } = default!;
        public DbSet<Gate> Gates { get; set; } = default!;
        public DbSet<Runway> Runways { get; set; } = default!;
        public DbSet<BaggageConveyorBelt> BaggageConveyorBelts { get; set; } = default!;
    }
}