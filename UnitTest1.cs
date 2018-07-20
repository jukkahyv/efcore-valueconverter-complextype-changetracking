using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace EfJsonTest {

    public class MyComplexType {
        public string Field { get; set; }
        public override bool Equals(object obj) {
            if (obj is MyComplexType t) {
                return (Field == t.Field);
            } else {
                return false;
            }
        }
        public override int GetHashCode() {
            return Field.GetHashCode();
        }
    }
    public class MyEntity {
        public int Id { get; set; }
        public string Simple { get; set; }
        public MyComplexType Complex { get; set; }
    }

    public class TestDbContext : DbContext {

      public DbSet<MyEntity> MyEntities { get; set; }

      protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseInMemoryDatabase("Test");
      }

      protected override void OnModelCreating(ModelBuilder modelBuilder) {
        var myComplexTypeValueComparer = new ValueComparer<MyComplexType>((t1, t2) => t1.Equals(t2), t => t.GetHashCode(), t => new MyComplexType { Field = t.Field });
        modelBuilder.Entity<MyEntity>().Property(p => p.Complex).Metadata.SetValueComparer(myComplexTypeValueComparer);
        modelBuilder.Entity<MyEntity>().Property(p => p.Complex)
            .HasConversion(c => JsonConvert.SerializeObject(c), c => JsonConvert.DeserializeObject<MyComplexType>(c));
      }

    }

    [TestClass]
    public class UnitTest1 {
        [TestMethod]
        public void TestMethod1() {

            using (var db = new TestDbContext()) {

                var entity = new MyEntity { Complex = new MyComplexType { Field = "Value 1 "}};
                db.Add(entity);
                db.SaveChanges();

                db.Entry(entity).State = EntityState.Detached;
                entity = db.MyEntities.Find(entity.Id);

                entity.Complex.Field = "Value 2";

                db.ChangeTracker.DetectChanges(); // Just in case

                Assert.IsTrue(db.Entry(entity).Property(p => p.Complex).IsModified, "Property is modified"); // This fails
                Assert.AreEqual(EntityState.Modified, db.Entry(entity).State, "Entity is modified"); // This also fails

            }

        }
    }

}
