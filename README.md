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

3. Ensure AutoHistory in DbContext. This must be called before `bloggingContext.SaveChanges()` or `bloggingContext.SaveChangesAsync()`.

```csharp
bloggingContext.EnsureAutoHistory()
```

If you want to record data changes for all entities (except for Added - entities), just override `SaveChanges` and `SaveChangesAsync` methods and call `EnsureAutoHistory()` inside overridden version:
```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options)
        : base(options)
    { }
    
    public override int SaveChanges()
    {
        this.EnsureAutoHistory();
        return base.SaveChanges();
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        this.EnsureAutoHistory();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // enable auto history functionality.
        modelBuilder.EnableAutoHistory();
    }
}
```
4. If you also want to record Added - Entities, which is not possible per default, override `SaveChanges` and `SaveChangesAsync` methods this way:
```csharp
public class BloggingContext : DbContext
{
    public override int SaveChanges()
    {
        var addedEntities = this.ChangeTracker
                                .Entries()
                                .Where(e => e.State == EntityState.Added)
                                .ToArray(); // remember added entries,
        // before EF Core is assigning valid Ids (it does on save changes, 
        // when ids equal zero) and setting their state to 
        // Unchanged (it does on every save changes)
        this.EnsureAutoHistory();
        base.SaveChanges();

        // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
        // and the history for them can be ensured and be saved with another "SaveChanges"
        this.EnsureAddedHistory(addedEntities);
        base.SaveChanges();
    }   
}
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

# Excluding properties from AutoHistory
You can now excluded properties from being saved into the AutoHistory tables by adding a custom attribute[ExcludeFromHistoryAttribute] attribute to your model properties. 


```csharp
    public class Blog
    {        
        [ExcludeFromHistory]
        public string PrivateURL { get; set; }
    }
```

# Integrate AutoHistory into other Package

[Microsoft.EntityFrameworkCore.UnitOfWork](https://github.com/lovedotnet/UnitOfWork) had integrated this package.



