using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoneVisQA.API;

/// <summary>
/// Bỏ yêu cầu Bearer token cho các endpoint Auth công khai.
/// </summary>
public class SwaggerAuthFilter : IOperationFilter
{
    private static readonly string[] NoAuthEndpoints = { "register", "login", "forgot-password", "reset-password" };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionName = context.ApiDescription.RelativePath?.ToLowerInvariant() ?? "";
        if (NoAuthEndpoints.Any(e => actionName.Contains(e)))
        {
            operation.Security = new List<OpenApiSecurityRequirement>();
        }
    }
}
