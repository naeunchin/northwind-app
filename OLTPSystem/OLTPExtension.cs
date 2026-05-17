using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.DAL;

namespace OLTPSystem
{
    public static class OLTPExtension
    {
        public static IServiceCollection OLTPDependencies(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
        {
            services.AddDbContext<NorthwindContext>(options);

            return services;
        }
    }
}
