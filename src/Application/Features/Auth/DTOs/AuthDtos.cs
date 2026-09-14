namespace FlowDesk.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? JobTitle,
    Guid? DepartmentId,
    List<string> Roles,
    List<string> Permissions
);
