using AcademicService.Domain.Enums;
using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AcademicService.Domain.Entities
{
    public class StudentPaper : Entity
    {
        public Guid ExamCampaignId { get; set; }
        public string? StudentIdentity { get; set; } // Mã HS (có thể ẩn/mã hóa nếu chấm phách)/ Hoặc name file
        public PaperStatus Status { get; set; }

        private readonly List<PaperDocument> _documents = new();
        public IReadOnlyCollection<PaperDocument> Documents => _documents.AsReadOnly();
    }

}

