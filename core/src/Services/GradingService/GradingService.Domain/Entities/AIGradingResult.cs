using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace GradingService.Domain.Entities
{
    public class AIGradingResult : Entity
    {
        public Guid StudentPaperId { get; private set; }

        // Nếu điểm này có thể bị reject bởi hệ thống rule-based trước khi show
        public bool IsValidResult { get; private set; }

        private readonly List<AIQuestionSuggestion> _suggestions = new();
        public IReadOnlyCollection<AIQuestionSuggestion> Suggestions => _suggestions.AsReadOnly();
    }
}
