using System.Text.Json;
using Amanah.Api.Models.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Amanah.Api.Filters;

public sealed class ApiValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        context.Result = new BadRequestObjectResult(
            new ApiError(ErrorCodes.ValidationFailed, "Please correct the errors in the form.", errors));
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
