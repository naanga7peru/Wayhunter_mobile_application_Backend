using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wayhunter.Api.Controllers.Mobile.Games;

namespace Wayhunter.Api.Extensions;

public sealed class SwaggerBearerOperationFilter : IOperationFilter
{
    public const string Scheme = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var action = context.ApiDescription.ActionDescriptor;
        var usesGameDraftFilter = action.FilterDescriptors.Any(descriptor =>
            descriptor.Filter is ServiceFilterAttribute filter &&
            filter.ServiceType == typeof(GameDraftAuthenticationFilter));
        var requiresAuthorization =
            action.EndpointMetadata.OfType<IAuthorizeData>().Any() &&
            !action.EndpointMetadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization && !usesGameDraftFilter)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(Scheme, context.Document)] = []
        });

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        if (action.EndpointMetadata.OfType<IAuthorizeData>().Any(data =>
                !string.IsNullOrEmpty(data.Policy) || !string.IsNullOrEmpty(data.Roles)))
        {
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
        }
    }
}
