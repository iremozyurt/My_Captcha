using CaptchaDemo.Models;
using Microsoft.EntityFrameworkCore;
namespace CaptchaDemo
{
    

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Ek veri yapılandırmaları burada yapılabilir
        }
    }

}
