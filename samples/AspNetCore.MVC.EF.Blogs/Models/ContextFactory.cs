using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EFGetStarted.AspNetCore.NewDb.Models
{
    /// <summary>
    /// Class need for EF Migrations to know how the Context should be created.
    /// This class is not intended for use on the applications.
    /// </summary>
    public class BloggingContextFactory : IDesignTimeDbContextFactory<BloggingContext>
    {
        /// <summary>
        /// Creates a DbOptionsBuilder from a connectionString.
        /// </summary>
        /// <param name="connectionString">ConnectionString to apply.</param>
        /// <returns>DbContextOptionsBuilder.</returns>
        public static DbContextOptionsBuilder CreateDbOptionsBuilder(string connectionString)
        {
            var options = new DbContextOptionsBuilder();
            return SetDbOptions(options, connectionString);
        }

        /// <summary>
        /// Set options to a DbOptionsBuilder.
        /// </summary>
        /// <param name="options">Options.</param>
        /// <param name="connectionString">ConnectionString to apply.</param>
        /// <returns>DbContextOptionsBuilder.</returns>
        public static DbContextOptionsBuilder SetDbOptions(DbContextOptionsBuilder options, string connectionString)
        {
            return options.UseSqlServer(connectionString);
        }


        /// <summary>
        /// Create a new DB Context, not intended to be used.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public BloggingContext CreateDbContext(string[] args)
        {
            var connection = @"Server=.;Database=AutoHistoryTest;Trusted_Connection=True;ConnectRetryCount=0";
            var optionsBuilder = CreateDbOptionsBuilder(connection);
            return new BloggingContext(optionsBuilder.Options);
        }
    }
}
