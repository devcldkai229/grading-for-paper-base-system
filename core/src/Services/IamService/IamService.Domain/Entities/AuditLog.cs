using System;
using Contracts.Domain;

namespace IamService.Domain.Entities
{
    public class AuditLog : Entity
    {
        public Guid? UserId { get; set; } // Admin ID performing the change, or null if system/anonymous
        public string Action { get; set; } = string.Empty; // e.g. "Create", "Update", "Deactivate", "SoftDelete", "HardDelete"
        public string EntityType { get; set; } = string.Empty; // "User"
        public Guid EntityId { get; set; } // ID of the user being changed
        public string? OldValue { get; set; } // JSON or simple representation of old state
        public string? NewValue { get; set; } // JSON or simple representation of new state
    }
}
