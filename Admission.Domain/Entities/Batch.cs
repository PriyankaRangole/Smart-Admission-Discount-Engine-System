namespace Admission.Domain.Entities
{
    public class Batch
    {
        public Guid BatchId {  get; set; }
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public Guid StudentId { get; set; }
        public Student Student { get; set; }

        public string BatchCode { get; set; } = string.Empty;
        public string Location {  get; set; }
        public string Mode { get; set; }

        public int Capacity { get; set; }

        public DateTime StartDate {  get; set; }
        public DateTime EndDate { get; set; }

        public decimal fees { get; set; }
        public bool isActive { get; set; }

        public ICollection<Registration> registrations { get; set; }=new List<Registration>();

    }
}