using Api.Models;
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
            //"Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;"
        }
    }
}