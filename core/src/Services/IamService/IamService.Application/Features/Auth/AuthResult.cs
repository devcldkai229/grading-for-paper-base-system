using System;
using System.Collections.Generic;

namespace IamService.Application.Features.Auth
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
