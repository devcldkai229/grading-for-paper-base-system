using Contracts.Domain;
using Contracts.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace GradingService.Domain.Entities
{
    public class GradingAssignment : Entity
    {
        public Guid StudentPaperId { get; private set; }
        public Guid TeacherId { get; private set; }
        public AssignmentType Type { get; private set; } // Lần đầu hay Phúc khảo
        public GradingProgressStatus Status { get; private set; }
        public AIGradingStatus AiStatus { get; private set; }

    }
}
