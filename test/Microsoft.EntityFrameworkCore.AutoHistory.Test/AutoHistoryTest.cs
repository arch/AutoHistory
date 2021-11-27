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
                    },
                    PrivateURL = "http://www.secret.com"
                }); ;
                db.EnsureAutoHistory();

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
                    },
                    PrivateURL = "http://www.secret.com"
                };
                db.Attach(blog);
                db.SaveChanges();

                // nullable fix?
                blog.Posts[0].NumViews = 10;
                db.EnsureAutoHistory();
                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Modified);

                Assert.Equal(1, count);
            }
        }


        [Fact]
        public void Entity_Delete_AutoHistory_Test()
        {
            using var db = new BloggingContext();
            var blog = new Blog
            {
                Url = "http://blogs.msdn.com/adonet",
                Posts = new List<Post> {
                        new Post {
                            Title = "xUnit",
                            Content = "Delete Post from xUnit test."
                        }
                    },
                PrivateURL = "http://www.secret.com"
            };
            db.Attach(blog);
            db.SaveChanges();


            db.Remove(blog);
            db.EnsureAutoHistory();
            var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Deleted);

            // blog and post are deleted
            Assert.Equal(2, count);
        }
    }
}
