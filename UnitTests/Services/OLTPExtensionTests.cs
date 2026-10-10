using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem;
using OLTPSystem.BLL;
using OLTPSystem.DAL;

namespace UnitTests.Services
{
    public class OLTPExtensionTests
    {
        [Fact]
        public void OLTPDependencies_RegistersServicesThatResolveFromTheContainer()
        {
            // Arrange - ValidateOnBuild fails fast if any registration has a missing or mismatched dependency
            var services = new ServiceCollection();
            services.OLTPDependencies(options => options.UseSqlite("DataSource=:memory:"));

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            using var scope = provider.CreateScope();

            // Act & Assert
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<NorthwindContext>>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<OrderService>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ProductService>().Should().NotBeNull();
        }
    }
}
