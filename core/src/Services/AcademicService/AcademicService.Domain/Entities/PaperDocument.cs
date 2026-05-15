using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AcademicService.Domain.Entities
{
    public class PaperDocument : Entity
    {
        public Guid StudentPaperId { get; private set; }
        public string? OriginalFileName { get; private set; }
        public string? OriginalFileUrl { get; private set; }
    }
}
