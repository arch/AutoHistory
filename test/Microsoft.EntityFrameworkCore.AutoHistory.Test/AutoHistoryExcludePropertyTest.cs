using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.AutoHistory.Test
{
    public class AutoHistoryExcludePropertyTest
    {
        [Fact]
        public void Entity_Update_AutoHistory_Exclude_Only_Modified_Property_Changed_Test()
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
                    },
                    PrivateURL = "http://www.secret.com"
                };

                db.Attach(blog);
                db.SaveChanges();

                blog.PrivateURL = "http://new.secret.com";
                db.EnsureAutoHistory();

                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);

                //No changes are made (excluded is the only property modified)
                Assert.Equal(0, count);
            }
        }

        [Fact]
        public void Entity_Update_AutoHistory_Exclude_Changed_Test()
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
                    },
                    PrivateURL = "http://www.secret.com"
                };

                db.Attach(blog);
                db.SaveChanges();

                blog.PrivateURL = "http://new.secret.com";
                blog.Posts[0].NumViews = 10;
                db.EnsureAutoHistory();

                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
                
                Assert.Equal(1, count);
            }
        }
    }
}