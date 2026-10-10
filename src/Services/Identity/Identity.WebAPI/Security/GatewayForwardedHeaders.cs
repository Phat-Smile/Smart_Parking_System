using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ParkingSystem.Identity.WebAPI.Security;

// Request tới Identity đi qua Gateway nên RemoteIpAddress là IP của Gateway; IP thật của người dùng nằm ở X-Forwarded-For.
// Chỉ tin header này khi request đến từ loopback hoặc mạng nội bộ (nơi Gateway chạy), để client ngoài không giả IP được.
internal static class GatewayForwardedHeaders
{
    private static readonly string[] TrustedNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"];

    public static IServiceCollection AddGatewayForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            // Gateway là proxy duy nhất phía trước, chỉ lấy một địa chỉ cuối cùng do nó thêm vào.
            options.ForwardLimit = 1;
            foreach (var network in TrustedNetworks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
}
