using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace GradingService.Domain.Entities
{
    public class GradingForm : Entity
    {
        public Guid GradingAssignmentId { get; set; }

        public decimal TotalScore { get; set; }

        public string? PaperComment { get; set; } // Nhận xét chung về bài làm (có thể có hoặc không)
        public string? GeneralComment { get; set; }  // Nhận xét tổng thể
        public string? InternalComment { get; set; } // Nhận xét nội bộ (chỉ giáo viên/admin thấy)

        readonly List<QuestionGradeDetail> _questionGrades = new();
        public IReadOnlyCollection<QuestionGradeDetail> QuestionGrades => _questionGrades.AsReadOnly();
    }
}
