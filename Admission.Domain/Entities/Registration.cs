using Admission.Domain.Common;
using Admission.Domain.Enums;

namespace Admission.Domain.Entities
{
    public class Registration:BaseEntity
    {

       
            public Guid RegistrationId { get; private set; }

            public Guid StudentId { get; private set; }
            public Student Student { get; private set; } = null!;

            public Guid BatchId { get; private set; }
            public Batch Batch { get; private set; } = null!;

            public Guid CourseId { get; private set; } // snapshot

            public decimal BaseFee { get; private set; }
            public decimal DiscountAmount { get; private set; }
            public decimal FinalPayableAmount { get; private set; }

            public string? CouponCodeApplied { get; private set; }

            public string? ReceiptId { get; private set; }

            public RegistrationStatus Status { get; private set; }

            public DateTime CreatedOn { get; private set; }

            private Registration() { } // EF Core

            public Registration(
                Guid studentId,
                Guid batchId,
                Guid courseId,
                decimal baseFee)
            {
                if (baseFee <= 0)
                    throw new ArgumentException("Base fee must be positive.");

                RegistrationId = Guid.NewGuid();
                StudentId = studentId;
                BatchId = batchId;
                CourseId = courseId;

                BaseFee = baseFee;
                DiscountAmount = 0;
                FinalPayableAmount = baseFee;

                Status = RegistrationStatus.Reserved;
                CreatedOn = DateTime.UtcNow;
            }

            public void ApplyDiscount(decimal discount, string? couponCode = null)
            {
                if (discount < 0)
                    throw new ArgumentException("Discount cannot be negative.");

                if (discount > BaseFee)
                    throw new InvalidOperationException("Discount cannot exceed base fee.");

                DiscountAmount = discount;
                CouponCodeApplied = couponCode;
                FinalPayableAmount = BaseFee - DiscountAmount;
            }

            public void ConfirmPayment(string receiptId)
            {
                if (Status != RegistrationStatus.Reserved)
                    throw new InvalidOperationException("Only reserved registrations can be confirmed.");

                if (string.IsNullOrWhiteSpace(receiptId))
                    throw new ArgumentException("ReceiptId is required.");

                ReceiptId = receiptId;
                Status = RegistrationStatus.Confirmed;
            }

            public void Cancel()
            {
                if (Status == RegistrationStatus.Cancelled)
                    throw new InvalidOperationException("Already cancelled.");

                Status = RegistrationStatus.Cancelled;
            }
        }
}