using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ParkingSystem.Gateway.Security;

internal sealed class GatewayServiceKeyTransform(IOptions<ServiceKeyOptions> options) : ITransformProvider
{
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        var key = options.Value.ServiceKey;
        if (!string.IsNullOrEmpty(key))
            context.AddRequestHeader(ServiceKeyValidationMiddleware.HeaderName, key, append: false);

        context.AddRequestTransform(transformContext =>
        {
            var correlationId = transformContext.HttpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
            if (!string.IsNullOrEmpty(correlationId))
            {
                transformContext.ProxyRequest.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
            }
            return ValueTask.CompletedTask;
        });
    }
}
