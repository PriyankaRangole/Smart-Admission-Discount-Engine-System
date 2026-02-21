using Admission.Domain.Common;
using Admission.Domain.Enums;

namespace Admission.Domain.Entities
{
    public class Registration:BaseEntity
    {
       
            public Guid RegistrationId { get; set; }

            public Guid StudentId { get; set; }
            public Student Student { get; set; } = null!;

            public Guid BatchId { get; set; }
            public Batch Batch { get; set; } = null!;

            public Guid CourseId { get; set; } // snapshot

            public decimal BaseFee { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal FinalPayableAmount { get; set; }

            public string? CouponCodeApplied { get; set; }

            public RegistrationStatus Status { get; set; }
                = RegistrationStatus.Reserved;
        }
}