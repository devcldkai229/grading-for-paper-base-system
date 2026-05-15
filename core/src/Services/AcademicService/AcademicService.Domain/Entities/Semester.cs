using Contracts.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AcademicService.Domain.Entities
{
    public class Semester : Entity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; }

    }
}
