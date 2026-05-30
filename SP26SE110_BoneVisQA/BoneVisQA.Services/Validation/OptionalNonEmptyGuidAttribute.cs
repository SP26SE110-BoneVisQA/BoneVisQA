using System.ComponentModel.DataAnnotations;

namespace BoneVisQA.Services.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class OptionalNonEmptyGuidAttribute : ValidationAttribute
{
    public OptionalNonEmptyGuidAttribute()
        : base("{0} must be a non-empty GUID.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
            return ValidationResult.Success;

        if (value is Guid guid)
            return guid != Guid.Empty
                ? ValidationResult.Success
                : new ValidationResult(FormatErrorMessage(validationContext.DisplayName));

        return new ValidationResult($"{validationContext.DisplayName} must be a valid GUID.");
    }
}
