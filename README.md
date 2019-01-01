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

3. Ensure AutoHistory in DbContext. This must be called before bloggingContext.SaveChanges() or bloggingContext.SaveChangesAsync().

```csharp
bloggingContext.EnsureAutoHistory()
```

# Use Custom AutoHistory Entity
You can use a custom auto history entity by extending the Microsoft.EntityFrameworkCore.AutoHistory class.

```csharp
class CustomAutoHistory : AutoHistory
{
    public String CustomField { get; set; }
}
```

Then register it in the db context like follows:
```csharp
modelBuilder.EnableAutoHistory<CustomAutoHistory>(o => { });
```

Then provide a custom history entity creating factory when calling EnsureAutoHistory. The example shows using the
factory directly, but you should use a service here that fills out your history extended properties(The properties inherited from `AutoHistory` will be set by the framework automatically).
```csharp
db.EnsureAutoHistory(() => new CustomAutoHistory()
                    {
                        CustomField = "CustomValue"
                    });
```

# Integrate AutoHistory into other Package

[Microsoft.EntityFrameworkCore.UnitOfWork](https://github.com/lovedotnet/UnitOfWork) had integrated this package.



