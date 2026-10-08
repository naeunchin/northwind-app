using System;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;
using OLTPSystem.Entities;

namespace UnitTests.Services
{
    /// <summary>
    /// SQLite-compatible NorthwindContext for tests. SQLite shares one namespace between tables and indexes,
    /// so the Customers index named "Region" collides with the Region table. SQL Server is unaffected.
    /// </summary>
    internal class TestNorthwindContext : NorthwindContext
    {
        public TestNorthwindContext(DbContextOptions<NorthwindContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Rename the existing named index from [Index("Region", Name = "Region")] rather than adding a second one
            var index = modelBuilder.Entity<Customer>().Metadata.FindIndex("Region")
                ?? throw new InvalidOperationException("Expected [Index(Name = \"Region\")] on Customer; update TestNorthwindContext.");

            index.SetDatabaseName("IX_Customers_Region");
        }
    }
}
