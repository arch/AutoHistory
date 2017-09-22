using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.AutoHistory.Test
{
    public class AutoHistoryTest
    {
        [Fact]
        public void Entity_Add_AutoHistory_Test()
        {
            using (var db = new BloggingContext())
            {
                db.Blogs.Add(new Blog
                {
                    Url = "http://blogs.msdn.com/adonet",
                    Posts = new List<Post> {
                        new Post {
                            Title = "xUnit",
                            Content = "Post from xUnit test."
                        }
                    }
                });

                db.EnsureAutoHistory("pepito@gmail.com", "127.0.0.1");

                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
                

                Assert.Equal(2, count);
            }
        }
        [Fact]
        public void Entity_Update_AutoHistory_Test()
        {
            using (var db = new BloggingContext())
            {
                var blog = new Blog
                {
                    Url = "http://blogs.msdn.com/adonet",
                    Posts = new List<Post> {
                        new Post {
                            Title = "xUnit",
                            Content = "Post from xUnit test."
                        }
                    }
                };
                db.Attach(blog);
                db.SaveChanges();

                blog.Posts[0].Content = "UpdatedPost";
                db.EnsureAutoHistory(null, "xxx@example.com");
                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);

                Assert.Equal(1, count);
            }
        }
    }
}
