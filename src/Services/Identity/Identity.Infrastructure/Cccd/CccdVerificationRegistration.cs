using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Cccd;

namespace ParkingSystem.Identity.Infrastructure.Cccd;

public static class CccdVerificationRegistration
{
    public static IServiceCollection AddCccdVerification(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(VneidApiOptions.SectionName);
        var provider = section["Provider"] ?? VneidApiOptions.StubProvider;

        if (string.Equals(provider, VneidApiOptions.HttpProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<VneidApiOptions>()
                .Bind(section)
                .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "VneidApi:BaseUrl is required when Provider is Http.")
                .ValidateOnStart();

            services.AddHttpClient<ICccdVerifier, VneidCccdVerifier>((serviceProvider, client) =>
            {
                var opts = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<VneidApiOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else
        {
            services.AddSingleton<ICccdVerifier, StubCccdVerifier>();
        }

        return services;
    }
}
