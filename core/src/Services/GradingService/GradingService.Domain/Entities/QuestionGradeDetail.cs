using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace GradingService.Domain.Entities
{
    public class QuestionGradeDetail : Entity
    {
        public Guid GradingFormId { get; set; }
        public string QuestionNumber { get; set; } 

        public decimal Score { get; set; }

        public decimal MaxScore { get; set; } // Điểm tối đa của câu hỏi này (để giáo viên dễ dàng so sánh)

        public string? QuestionComment { get; set; } // Nhận xét bài làm (cho riêng câu này)
    }
}
