using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Users;

public record CreateUserRequest(long? TenantId, string FullName, string Email, string? Phone, string Role, string Password);

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] Allowed = RoleNames.TenantRoles.Concat([RoleNames.SuperAdmin]).ToArray();

    public CreateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Role).Must(r => Allowed.Contains(r)).WithMessage("Invalid role name.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public record UpdateUserRequest(string FullName, string? Phone, string? Role, bool IsActive);

public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Role).Must(r => r == null ||
                RoleNames.TenantRoles.Append(RoleNames.SuperAdmin).Contains(r))
            .WithMessage("Invalid role name.");
    }
}

public record AdminResetPasswordRequest(string NewPassword);

public class AdminResetPasswordValidator : AbstractValidator<AdminResetPasswordRequest>
{
    public AdminResetPasswordValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
