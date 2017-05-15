using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.AutoHistory.Test {
    public class AutoHistoryTest {
        [Fact]
        public void Entity_Add_AutoHistory_Test() {
            using (var db = new BloggingContext()) {
                db.Blogs.Add(new Blog {
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

                Assert.Equal(4, count);
            }
        }
    }
}
