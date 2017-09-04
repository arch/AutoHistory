# AutoHistory
A plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.

# How to use

`AutoHistory` will recording all the data changing history in one `Table` named `AutoHistories`, this table will recording data
`UPDATE`, `DELETE` history.

1. Install AutoHistory Package

Run the following command in the `Package Manager Console` to install Microsoft.EntityFrameworkCore.AutoHistory

`PM> Install-Package Microsoft.EntityFrameworkCore.AutoHistory`

2. Enable AutoHistory

```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options)
        : base(options)
    { }

    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // enable auto history functionality.
        modelBuilder.EnableAutoHistory();
    }
}
```

3. Ensure AutoHistory in DbContext

```csharp
bloggingContext.EnsureAutoHistory()
```

# Integrate AutoHistory into other Package

[Microsoft.EntityFrameworkCore.UnitOfWork](https://github.com/lovedotnet/UnitOfWork) had integrated this package.



