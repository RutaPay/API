using Api.Models;
using Api.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data
{
    public class ApplicationDBContext : IdentityDbContext<User>
    {
        public ApplicationDBContext(DbContextOptions dbContextOptions) : base(dbContextOptions)
        {

        }
        public DbSet<Card> Cards { get; set; }
        public DbSet<Point> Points { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<VerificationRequest> VerificationRequests { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<UserReward> UserRewards { get; set; }
        public DbSet<Terminal> Terminals { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            List<IdentityRole> role = new List<IdentityRole>
            {
                new IdentityRole
                {
                    Id = "Admin",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "1"
                },
                new IdentityRole
                {
                    Id = "User",
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp = "2"
                },
                new IdentityRole
                {
                    Id = "Student",
                    Name = "Student",
                    NormalizedName = "STUDENT",
                    ConcurrencyStamp = "3"
                },
                new IdentityRole
                {
                    Id = "Health",
                    Name = "Health",
                    NormalizedName = "HEALTH",
                    ConcurrencyStamp = "4"
                },
                new IdentityRole
                {
                    Id = "Adult",
                    Name = "Adult",
                    NormalizedName = "ADULT",
                    ConcurrencyStamp = "5"
                },
                new IdentityRole
                {
                    Id = "Driver",
                    Name = "Driver",
                    NormalizedName = "DRIVER",
                    ConcurrencyStamp = "6"
                }
            };
            builder.Entity<IdentityRole>().HasData(role);

            List<Reward> defaultRewards = new List<Reward>
            {
                new Reward
                {
                    Id = 1,
                    Title = "Saldo: $50 MXN",
                    Description = "Recarga inmediata de $50 pesos a tu tarjeta digital RutaPay.",
                    PointsCost = 200,
                    RewardType = "Balance",
                    Value = 50.00m,
                    IsActive = true
                },
                new Reward
                {
                    Id = 2,
                    Title = "Saldo: $100 MXN",
                    Description = "Recarga inmediata de $100 pesos a tu tarjeta digital RutaPay.",
                    PointsCost = 350,
                    RewardType = "Balance",
                    Value = 100.00m,
                    IsActive = true
                },
                new Reward
                {
                    Id = 3,
                    Title = "Saldo: $200 MXN",
                    Description = "Recarga inmediata de $200 pesos a tu tarjeta digital RutaPay con bono de lealtad.",
                    PointsCost = 500,
                    RewardType = "Balance",
                    Value = 200.00m,
                    IsActive = true
                },
                new Reward
                {
                    Id = 4,
                    Title = "10 Boletos de Rifa Ecológica",
                    Description = "Participa en la rifa mensual por scooters eléctricos y kits sostenibles.",
                    PointsCost = 750,
                    RewardType = "Raffle",
                    Value = 0.00m,
                    IsActive = true
                }
            };
            builder.Entity<Reward>().HasData(defaultRewards);

            // ---- Terminales de pago a bordo de las unidades de transporte ----
            builder.Entity<Terminal>()
                .HasIndex(t => t.Code)
                .IsUnique();

            // Credenciales de demostración. Rótalas con
            // POST /api/Terminals/{id}/rotate-credential antes de producción.
            const string demoSalt = "rutapay-terminal-demo-salt-v1";
            var demoCreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            builder.Entity<Terminal>().HasData(
                new Terminal
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Code = "VAL-NFC-04",
                    BusUnitId = "UNIDAD-CEL-01",
                    RouteName = "Ruta General Celaya",
                    State = "Active",
                    CredentialSalt = demoSalt,
                    CredentialHash = HashService.Hash("rutapay-val-nfc-04-demo", demoSalt),
                    CreatedAt = demoCreatedAt
                },
                new Terminal
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Code = "VAL-QR-07",
                    BusUnitId = "UNIDAD-CEL-02",
                    RouteName = "Ruta Universidad",
                    State = "Active",
                    CredentialSalt = demoSalt,
                    CredentialHash = HashService.Hash("rutapay-val-qr-07-demo", demoSalt),
                    CreatedAt = demoCreatedAt
                });
        }
    }
}