using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace EfJsonTest {

    [TestClass]
    public class JsonComplexTypeTests {

        public class Address {

            public string Street { get; set; }

            public bool Equals(Address obj) {
                return Street == obj.Street;
            }

            public override int GetHashCode() {
                return Street?.GetHashCode() ?? 0;
            }

            public Address Clone() {
                return new Address { Street = Street };
            }

        }

        public class Customer {
            public int Id { get; set; }
            public Address Address { get; set; }
            public string Name { get; set; }
        }

        public class CustomerDbContext : DbContext {

            public DbSet<Customer> Customers { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
                base.OnConfiguring(optionsBuilder);
                optionsBuilder.UseInMemoryDatabase("Test");
                //optionsBuilder.UseSqlServer("Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=efcore-jsontest2;Integrated Security=True;MultipleActiveResultSets=True");
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder) {
             
                modelBuilder.Entity<Customer>().Property(m => m.Address).Metadata.SetValueComparer(new ValueComparer<Address>(
                    (p1, p2) => p1.Equals(p2),
                    p => p != null ? p.GetHashCode() : 0,
                    p => p != null ? p.Clone() : default));
                modelBuilder.Entity<Customer>().Property(m => m.Address)
                    .HasConversion(
                        v => v != null ? JsonConvert.SerializeObject(v) : null,
                        v => v != null ? JsonConvert.DeserializeObject<Address>(v) : default);
                
            }

        }

        private readonly CustomerDbContext db = new CustomerDbContext();

        private void AssertModified(Customer customer) {
            db.ChangeTracker.DetectChanges();
            Assert.AreEqual(EntityState.Modified, db.Entry(customer).State, "Entity state");
        }

        private void AssertAddressModified(Customer customer) {
            Assert.IsTrue(db.Entry(customer).Property(m => m.Address).IsModified, "Address is modified");
        }

        public JsonComplexTypeTests() {
            db.Database.EnsureCreated();
        }

        /// <summary>
        /// PASSES
        /// </summary>
        [TestMethod]
        public void PlainField() {

            var customer = new Customer { Name = "Customer" };
            db.Add(customer);
            db.SaveChanges();

            customer.Name = "Updated";
            AssertModified(customer);

        }

        /// <summary>
        /// FAILS
        /// </summary>
        [TestMethod]
        public void ComplexField_WithoutReload() {

            var customer = new Customer { Address = new Address { Street = "Street" } };
            db.Add(customer);
            db.SaveChanges();

            customer.Address.Street = "Updated";
            AssertModified(customer);
            AssertAddressModified(customer);

        }
        
        /// <summary>
        /// PASSES
        /// </summary>
        [TestMethod]
        public void ComplexField_WithReload() {

            var customer = new Customer { Address = new Address { Street = "Street" } };
            db.Add(customer);
            db.SaveChanges();

            db.Entry(customer).State = EntityState.Detached;
            customer = db.Customers.Find(customer.Id);

            customer.Address.Street = "Updated";
            AssertModified(customer);
            AssertAddressModified(customer);

        }

        /// <summary>
        /// FAILS
        /// </summary>
        [TestMethod]
        public void ComplexField_WithReloadAndModify() {

            var customer = new Customer { Address = new Address { Street = "Street" } };
            db.Add(customer);
            db.SaveChanges();

            db.Entry(customer).State = EntityState.Detached;
            customer = db.Customers.Find(customer.Id);

            customer.Address.Street = "Updated";
            db.SaveChanges();

            customer.Address.Street = "Updated 2";

            AssertModified(customer);
            AssertAddressModified(customer);

        }

        /// <summary>
        /// PASSES
        /// </summary>
        [TestMethod]
        public void ComplexField_WithAssign() {

            var customer = new Customer { Address = new Address { Street = "Street" } };
            db.Add(customer);
            db.SaveChanges();

            customer.Address = new Address { Street = "Updated" };

            AssertModified(customer);
            AssertAddressModified(customer);

        }

    }

}
