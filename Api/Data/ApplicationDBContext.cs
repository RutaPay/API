using Api.Models;
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


        //"Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;"
    }
}
