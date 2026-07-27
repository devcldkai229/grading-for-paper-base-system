using System;
using IamService.Domain.Enums;

namespace IamService.Application.Features.Users
{
    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? MarkerCode { get; set; }
        public UserRole Role { get; set; } = UserRole.Lecturer;
    }

    public class UpdateUserRequest
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MarkerCode { get; set; }
        public UserRole? Role { get; set; }
        public UserStatus? Status { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MarkerCode { get; set; }
        public string? AvatarUrl { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class PagedResult<T>
    {
        public System.Collections.Generic.IEnumerable<T> Items { get; set; } = new System.Collections.Generic.List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
