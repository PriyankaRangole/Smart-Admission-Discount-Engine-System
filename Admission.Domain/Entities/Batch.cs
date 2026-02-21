namespace Admission.Domain.Entities
{
    public class Batch
    {
        public int BatchId { get; private set; }

        public int CourseId { get; private set; }

        public string Location { get; private set; }

        public int Capacity { get; private set; }

        public int CurrentStrength { get; private set; }

        public decimal FeeAmount { get; private set; }

        public DateTime StartDate { get; private set; }

        public DateTime EndDate { get; private set; }

        private Batch() { }

        public Batch(
            int courseId,
            string location,
            int capacity,
            decimal feeAmount,
            DateTime startDate,
            DateTime endDate)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.");

            if (feeAmount <= 0)
                throw new ArgumentException("Fee must be greater than zero.");

            if (endDate <= startDate)
                throw new ArgumentException("End date must be after start date.");

            CourseId = courseId;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Capacity = capacity;
            FeeAmount = feeAmount;
            StartDate = startDate;
            EndDate = endDate;
            CurrentStrength = 0;
        }

        public void IncreaseStrength()
        {
            if (DateTime.UtcNow > EndDate)
                throw new InvalidOperationException("Batch registration period is over.");

            if (CurrentStrength >= Capacity)
                throw new InvalidOperationException("Batch is full.");

            CurrentStrength++;
        }

        public void DecreaseStrength()
        {
            if (CurrentStrength <= 0)
                throw new InvalidOperationException("No students to remove.");

            CurrentStrength--;
        }

        public void UpdateCapacity(int newCapacity)
        {
            if (newCapacity < CurrentStrength)
                throw new InvalidOperationException("Capacity cannot be less than confirmed students.");

            Capacity = newCapacity;
        }

        public void UpdateDates(DateTime newStart, DateTime newEnd)
        {
            if (newEnd <= newStart)
                throw new ArgumentException("End date must be after start date.");

            StartDate = newStart;
            EndDate = newEnd;
        }
    }
}