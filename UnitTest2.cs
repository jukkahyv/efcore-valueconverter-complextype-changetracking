using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Linq.Expressions;

namespace EfJsonTest {

    interface ICustomValue<T> {
        bool Equals(T other);
        T Snapshot();
    }

    public class MyComplexType2 : ICustomValue<MyComplexType2> {
        public string Field { get; set; }
        public bool Equals(MyComplexType2 obj) {
            return Field == obj.Field;
        }
        public override int GetHashCode() {
            return Field.GetHashCode();
        }

        public MyComplexType2 Snapshot() {
            return new MyComplexType2 { Field = Field };
        }
    }
    public class MyEntity2 {
        public int Id { get; set; }
        public MyComplexType2 Complex { get; set; }
    }

    public class TestDbContext2 : DbContext {

        private static JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings();

        private static void MapJsonbValue<TEntity, TValue>(ModelBuilder modelBuilder, Expression<Func<TEntity, TValue>> propertyExpression) where TEntity : class where TValue : ICustomValue<TValue> {
            modelBuilder.Entity<TEntity>().Property(propertyExpression).HasColumnType("jsonb");
            modelBuilder.Entity<TEntity>().Property(propertyExpression).Metadata.SetValueComparer(new ValueComparer<TValue>(
                (p1, p2) => p1.Equals(p2),
                p => p != null ? p.GetHashCode() : 0,
                p => p != null ? p.Snapshot() : default));
            modelBuilder.Entity<TEntity>().Property(propertyExpression)
                .HasConversion(
                    v => v != null ? JsonConvert.SerializeObject(v, JsonSerializerSettings) : null,
                    v => v != null ? JsonConvert.DeserializeObject<TValue>(v, JsonSerializerSettings) : default);
        }

        public DbSet<MyEntity2> MyEntities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            base.OnConfiguring(optionsBuilder);
            //optionsBuilder.UseSqlServer("Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=efcore-jsontest;Integrated Security=True;MultipleActiveResultSets=True");
            optionsBuilder.UseInMemoryDatabase("Test");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            MapJsonbValue<MyEntity2, MyComplexType2>(modelBuilder, e => e.Complex);
        }

    }

    [TestClass]
    public class UnitTest2 {

        [TestMethod]
        public void TestMethod1() {

            using (var db = new TestDbContext2()) {

                db.Database.EnsureCreated();

                var entity = new MyEntity2 { Complex = new MyComplexType2 { Field = "Value 1"}};
                db.Add(entity);
                db.SaveChanges();

                //db.Entry(entity).State = EntityState.Detached;
                //entity = db.MyEntities.Find(entity.Id);

                entity.Complex.Field = "Value 2";
                //entity.Complex = new MyComplexType2 { Field = "Value 2"}; // With this it works
                db.ChangeTracker.DetectChanges(); // Just in case

                Assert.IsTrue(db.Entry(entity).Property(p => p.Complex).IsModified, "Property is modified"); // This fails
                Assert.AreEqual(EntityState.Modified, db.Entry(entity).State, "Entity is modified"); // This also fails

            }


        }

    }

}
