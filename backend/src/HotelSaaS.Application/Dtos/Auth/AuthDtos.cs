using FluentValidation;

namespace HotelSaaS.Application.Dtos.Auth;

public record LoginRequest(string Email, string Password);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public record UserSummaryDto(long UserId, long? TenantId, string? TenantName, string FullName, string Email,
    string? Phone, IReadOnlyList<string> Roles, bool IsActive, bool MustChangePassword, DateTime? LastLoginAt);

public record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc,
    UserSummaryDto User, bool MustChangePassword);

public record RefreshRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a special character.");
    }
}

public record ForgotPasswordRequest(string Email);

public record ForgotPasswordResponse(string Message, string? DevResetToken);

public record ResetPasswordRequest(string Token, string NewPassword);

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
