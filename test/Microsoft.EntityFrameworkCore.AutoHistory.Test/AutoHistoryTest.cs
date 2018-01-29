using System;
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
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
                var blog = new Blog
                {
                    Url = "http://blogs.msdn.com/adonet",
                    Posts = new List<Post> {
                        new Post {
                            Title = "xUnit",
                            Content = "Post from xUnit test.",
                            NumViews = 10
                        }
                    }
                };
                db.Attach(blog);
                db.SaveChanges();
                Assert.True(blog.Posts[0].NumViews != null);
                // nullable fix?: yes
                blog.Posts[0].NumViews = null;
                db.EnsureAutoHistory();
                var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Modified);
                Assert.Equal(1, count);
                db.SaveChanges();
            }



        }

        [Fact]
        public void Entity_Update_AutoHistory_Test2()
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
                
                Assert.Equal("xUnit", db.Posts.First().Title);

            
                db.Blogs.First().Posts.First().Title = "The new Era";
                db.EnsureAutoHistory();
                db.SaveChanges();
                int c = db.DbHistory().Count();
                //Testing if there is two history rows
                Assert.Equal(2, c);
                Assert.Equal("The new Era", db.Posts.First().Title);
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
                Assert.Equal(0, db.DbHistory().Count());

                var post = blog.Posts[0];
                post.NumViews = 10;
                db.EnsureAutoHistory();
                db.SaveChanges();
                Assert.Equal(1, db.DbHistory().Count());

                blog.Posts[0].Title = "The new Era";
                db.EnsureAutoHistory();
                db.SaveChanges();
                //Testing EnsureAutoHistory extension method: must report 2 changes
                Assert.Equal(2, db.DbHistory().Count());

                
                //Testing AutoHistoryRollback extension method: must report 2 changes                
                db.AutoHistoryRollback(ref post);
                Assert.Equal(2, db.SaveChanges());
                //Testing if the title of my first post is "xUnit" again
                Assert.Equal("xUnit", db.Posts.First().Title);
                Assert.Equal(1, db.DbHistory().Count());
                
                post.Title = "The Ice Age 2";
                post.NumViews = 20;
                db.EnsureAutoHistory();
                Assert.Equal(2, db.SaveChanges());
                //Testing Rollback extension method: returnig to the first state, must report 2 changes                
                db.AutoHistoryRollback(ref post);
                Assert.Equal("xUnit", post.Title);
            }
        }
        [Fact]
        public void Ensuring_AutoHistory_Supports_Rollback_Validation()
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
                var post = blog.Posts[0];
                //The below statements test if the expected exception is thrown.
                Assert.Throws<System.ArgumentNullException>(() => db.AutoHistoryRollback(ref post));
                //New added entity in the context without being saved throws exception
                db.Add(blog.Posts[0]);
                Assert.Throws<System.Exception>(() => db.AutoHistoryRollback(ref post));

                //Saving changes
                db.SaveChanges();                
            }
        }


    }
}
