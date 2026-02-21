using Admission.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Admission.Domain.Entities
{
    public class Student:BaseEntity
    {
        public Guid StudentId { get; set; }

        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public ICollection<Registration> registrations { get; set; } = new List<Registration>();

    }
}
