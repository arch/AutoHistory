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
                Assert.Equal(2, db.DbHistory().Count());
                Assert.Equal("The new Era",db.Posts.First().Title);

                Assert.Equal(1, db.DbHistory().First().Id);
                Assert.Equal(1, db.DbHistory().Last().ParentId);
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


                //Testing Rollback extension method
                int id = db.DbHistory().Last().Id;
                output.WriteLine(id.ToString());
                Assert.Equal(1,db.Rollback(id));



            }
        }


    }
}
