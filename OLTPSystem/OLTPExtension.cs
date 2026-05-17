using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.BLL;
using OLTPSystem.DAL;

namespace OLTPSystem
{
    public static class OLTPExtension
    {
        public static IServiceCollection OLTPDependencies(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
        {
            services.AddDbContext<NorthwindContext>(options);

            services.AddScoped<OrderService>((ServiceProvider) =>
            {
                var context = ServiceProvider.GetService<NorthwindContext>();
                return context == null ? throw new InvalidOperationException("NorthwindContext is not registered.") : new OrderService(context);
            });

            services.AddScoped<ProductService>((ServiceProvider) =>
            {
                var context = ServiceProvider.GetService<NorthwindContext>();
                return context == null ? throw new InvalidOperationException("NorthwindContext is not registered.") : new ProductService(context);
            });

            return services;
        }
    }
}
