using Amanah.Contracts.Requests.Browse;
using FluentValidation;

namespace Amanah.Api.Validators.Browse;

public sealed class BrowseReportsQueryValidator : AbstractValidator<BrowseReportsQuery>
{
    private static readonly string[] AllowedTypes = ["lost", "found"];

    public BrowseReportsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(query => query.Type)
            .Must(type => string.IsNullOrWhiteSpace(type)
                || AllowedTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Type must be lost or found.");

        RuleFor(query => query.DateTo)
            .GreaterThanOrEqualTo(query => query.DateFrom!.Value)
            .When(query => query.DateFrom.HasValue && query.DateTo.HasValue)
            .WithMessage("End date must be on or after start date.");
    }
}
