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
            // Blazor Server scopes live for the whole circuit, so services create a short-lived context per operation
            // instead of sharing one scoped context across every call the user makes
            services.AddDbContextFactory<NorthwindContext>(options);

            services.AddScoped<OrderService>();
            services.AddScoped<ProductService>();

            return services;
        }
    }
}
