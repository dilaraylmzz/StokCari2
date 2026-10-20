using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StokCari.Api.Infrastructure;

/// <summary>Swagger arayüzüne Admin için isteğe bağlı X-Company-Id başlığını ekler.</summary>
public class SwaggerCompanyHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HttpCurrentUser.CompanyHeader,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Yalnızca Admin: işlemin yapılacağı şirketin Id değeri. User rolünde yok sayılır (şirket token'dan alınır).",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
