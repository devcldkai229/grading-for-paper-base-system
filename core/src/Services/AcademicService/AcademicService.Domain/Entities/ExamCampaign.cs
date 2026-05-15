using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AcademicService.Domain.Entities
{
    public class ExamCampaign : Entity
    {
        public Guid SemesterId { get; private set; }
        public string ExamName { get; private set; } // Vd: PE #1, FE #2
        public string SubjectCode { get; private set; } // Vd: PMG201c
        public string? QuestionFileUrl { get; private set; } // Đề thi
        public string? RubricFileUrl { get; private set; }   // Barem file gốc 
        public string? ParsedRubricJson { get; private set; } 
        public DateTime StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }

    }
}
