using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Admission.Domain.Entities
{
    public class Course
    {
        public Guid CourseId { get; set; }

        public string CourseName { get; set; }

        public string Description { get; set; }

        public bool isActive { get; set; } = true;

        public ICollection<Batch> batches { get; set; } = new List<Batch>();

    }
}
