using System.Text.Json;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
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
            .Select(entry => new
            {
                Field = JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                Messages = entry.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToArray()
            })
            .Where(entry => entry.Messages.Length > 0)
            .ToDictionary(entry => entry.Field, entry => entry.Messages);

        context.Result = ResultError.BadRequest(
            "Please correct the errors in the form.",
            ErrorCodes.ValidationFailed,
            errors).ToActionResult();
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
