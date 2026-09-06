using Amanah.Api.Utilities.Admin;
using Amanah.Contracts.Requests.Admin;
using FluentValidation;

namespace Amanah.Api.Validators.Admin;

public sealed class CreateCategoryFieldRequestValidator : AbstractValidator<CreateCategoryFieldRequest>
{
    public CreateCategoryFieldRequestValidator()
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
            .WithMessage("Field type must be text or integer.");

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sort order must be zero or greater.");

        RuleFor(request => request.TextFormat)
            .Must(CategoryCatalogConstraints.IsValidTextFormat)
            .WithMessage("Text format is invalid.");

        RuleFor(request => request)
            .Must(request => CategoryCatalogConstraints.IsTextType(request.Type) || request.TextFormat is null)
            .WithMessage("Text format is only valid for text fields.");

        RuleFor(request => request)
            .Must(request => !CategoryCatalogConstraints.IsTextType(request.Type)
                || request.MinInt is null && request.MaxInt is null)
            .WithMessage("Integer bounds are only valid for integer fields.");

        RuleFor(request => request)
            .Must(request => CategoryCatalogConstraints.IsTextType(request.Type)
                || request.MinLength is null && request.MaxLength is null)
            .WithMessage("Length bounds are only valid for text fields.");

        RuleFor(request => request)
            .Must(request => request.MinLength is null
                || request.MaxLength is null
                || request.MinLength <= request.MaxLength)
            .WithMessage("Minimum length cannot exceed maximum length.");

        RuleFor(request => request)
            .Must(request => request.MinInt is null
                || request.MaxInt is null
                || request.MinInt <= request.MaxInt)
            .WithMessage("Minimum value cannot exceed maximum value.");
    }
}
