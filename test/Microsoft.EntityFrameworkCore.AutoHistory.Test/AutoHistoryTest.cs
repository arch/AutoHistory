using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore.AutoHistory.Test
{
    public class AutoHistoryTest
    {
        private readonly ITestOutputHelper output;

        public AutoHistoryTest(ITestOutputHelper output)
        {
            this.output = output;
        }

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
                    }
                };
                db.Attach(blog);
                db.SaveChanges();
                Assert.True(blog.Posts[0].NumViews == null);
                // nullable fix?
                blog.Posts[0].NumViews = null;
                db.EnsureAutoHistory();
                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Modified);
                Assert.Equal(0, count);
            }



        }

        [Fact]
        public void Ensuring_AutoHistory_Supports_Rollback_Test1()
        {
            using (var db = new BloggingContext())
            {
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
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

                blog.Posts[0].NumViews = 10;
                db.EnsureAutoHistory();
                db.SaveChanges();
                Assert.Equal(1,db.DbHistory().Count());
                Assert.Equal("xUnit", db.Posts.First().Title);


                blog.Posts[0].Title = "The new Era";
                db.EnsureAutoHistory();
                db.SaveChanges();
                //Testing if there is two history rows
                Assert.Equal(2, db.DbHistory().Count());
                Assert.Equal("The new Era",db.Posts.First().Title);

                Assert.Equal(1, db.DbHistory().First().Id);
                Assert.Equal(2, db.DbHistory().Last().Id);
            }
        }

        [Fact]
        public void Ensuring_AutoHistory_Supports_Rollback_Test2()
        {
            using (var db = new BloggingContext())
            {
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
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

                blog.Posts[0].NumViews = 10;
                db.EnsureAutoHistory();
                db.SaveChanges();

                blog.Posts[0].Title = "The new Era";
                db.EnsureAutoHistory();
                db.SaveChanges();
                //Testing if there is two history rows
                Assert.Equal(2, db.DbHistory().Count());

                //Testing Rollback extension method: must report 2 changes
                object id = blog.Posts[0].PostId;
                Assert.Equal(2, db.Rollback(blog.Posts[0], id));

                //Testing if the title of my first post is "xUnit" again
                Assert.Equal("xUnit",db.Posts.Find(id).Title);
                //Testing if now there is only one history row in AutoHistory table
                Assert.Equal(1, db.DbHistory().Count());



            }
        }


    }
}
