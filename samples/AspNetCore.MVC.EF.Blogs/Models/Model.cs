using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EFGetStarted.AspNetCore.NewDb.Models
{
    public class BloggingContext : DbContext
    {
        public BloggingContext(DbContextOptions options)
            : base(options)
        { }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<AutoHistory> AutoHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // enable auto history functionality.
            modelBuilder.EnableAutoHistory();
        }

    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }

        [ExcludeFromHistory]
        public string PrivateURL { get; set; }

        public ICollection<Post> Posts { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }
}
