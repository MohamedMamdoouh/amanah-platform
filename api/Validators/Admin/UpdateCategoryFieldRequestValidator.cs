using Amanah.Api.Utilities.Admin;
using Amanah.Contracts.Requests.Admin;
using FluentValidation;

namespace Amanah.Api.Validators.Admin;

public sealed class UpdateCategoryFieldRequestValidator : AbstractValidator<UpdateCategoryFieldRequest>
{
    public UpdateCategoryFieldRequestValidator()
    {
        RuleFor(request => request.FieldKey)
            .NotEmpty()
            .WithMessage("Field key is required.")
            .MaximumLength(40)
            .WithMessage("Field key must be at most 40 characters.")
            .Matches(CategoryCatalogConstraints.FieldKeyPattern)
            .WithMessage("Field key must use lowercase letters, numbers, and underscores.");

        RuleFor(request => request.Type)
            .NotEmpty()
            .WithMessage("Field type is required.")
            .Must(CategoryCatalogConstraints.IsValidFieldType)
            .WithMessage("Field type must be text.");

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sort order must be zero or greater.");

        RuleFor(request => request.TextFormat)
            .Must(CategoryCatalogConstraints.IsValidTextFormat)
            .WithMessage("Text format is invalid.");

        RuleFor(request => request)
            .Must(request => request.MinLength is null
                || request.MaxLength is null
                || request.MinLength <= request.MaxLength)
            .WithMessage("Minimum length cannot exceed maximum length.");
    }
}
