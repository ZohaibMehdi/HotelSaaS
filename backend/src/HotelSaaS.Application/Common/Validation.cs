using FluentValidation;

namespace HotelSaaS.Application.Common;

/// <summary>
/// Explicit server-side validation entry point. Backend validation is
/// authoritative (PRD rule 7); services call this before executing logic.
/// </summary>
public static class Validate
{
    public static void Input<T>(IValidator<T>? validator, T instance)
    {
        if (validator is null) return;
        var result = validator.Validate(instance);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException("Validation failed.", errors);
        }
    }
}

/// <summary>Money helpers shared by booking/invoice/payroll calculations.</summary>
public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
