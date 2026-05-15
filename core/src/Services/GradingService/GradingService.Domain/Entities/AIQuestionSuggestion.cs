using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace GradingService.Domain.Entities
{
    public class AIQuestionSuggestion : Entity
    {
        public Guid AIGradingResultId { get; private set; }
        public string QuestionNumber { get; private set; } // Map với Request 1, Request 2...

        public decimal SuggestedScore { get; private set; }

        // Giải thích cho giáo viên hiểu tại sao cho điểm này (Reasoning từ LLM)
        public string Explanation { get; private set; }

        // Trích dẫn nguồn từ file bài làm (Ví dụ: "Học sinh có nhắc đến constraints tài nguyên ở dòng X")
        public string? EvidenceExtracted { get; private set; }
    }
}
