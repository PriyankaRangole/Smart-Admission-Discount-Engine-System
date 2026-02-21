### Clean Architecture blueprint (.NET 8 + EF Core + Migrations + SQL Server)

## 1) Solution structure

Create a solution with 4 projects:

- **Sunbeam.Registration.Domain** (Class Library)
    - Entities, Enums, Domain rules, Interfaces (no EF Core)
- **Sunbeam.Registration.Application** (Class Library)
    - Use cases (commands/queries), DTOs, Validators, Interfaces
- **Sunbeam.Registration.Infrastructure** (Class Library)
    - EF Core DbContext, configurations, migrations, repository implementations
- **Sunbeam.Registration.Api** ([ASP.NET](http://ASP.NET) Core Web API)
    - Controllers, DI setup, Auth, Swagger

Dependencies:

- Api → Application, Infrastructure
- Infrastructure → Application, Domain
- Application → Domain
- Domain → (nothing)

---

## 2) Domain layer (entities + enums)

### Enums

```csharp
public enum RegistrationStatus
{
    Reserved = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3
}

public enum DiscountType
{
    Percent = 0,
    Flat = 1
}
```

### Entities (keep them simple POCOs)

```csharp
public class Student
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}

public class Course
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}

public class Batch
{
    public Guid BatchId { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string BatchCode { get; set; } = "";
    public decimal FeeAmount { get; set; }
    public int Capacity { get; set; }

    public string Mode { get; set; } = ""; // Online/Classroom/Hybrid
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}

public class Registration
{
    public Guid RegistrationId { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public Guid CourseId { get; set; } // snapshot
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Reserved;

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public decimal BaseFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPayableAmount { get; set; }

    public string? CouponCodeApplied { get; set; } // one coupon only
}

public class Discount
{
    public Guid DiscountId { get; set; }
    public string Name { get; set; } = "";
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinFeeAmount { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? MetadataJson { get; set; } // extendable rules
}

public class Coupon
{
    public string CouponCode { get; set; } = "";
    public Guid DiscountId { get; set; }
    public Discount Discount { get; set; } = null!;
    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerStudent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CouponUsage
{
    public Guid CouponUsageId { get; set; }
    public string CouponCode { get; set; } = "";
    public Guid RegistrationId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;
}
```

---

## 3) Application layer (use case contract)

### DTOs

```csharp
public record CreateRegistrationRequest(
    string Name,
    string Email,
    string? Phone,
    Guid BatchId,
    string? CouponCode
);

public record CreateRegistrationResponse(
    Guid RegistrationId,
    decimal BaseFee,
    decimal DiscountAmount,
    decimal FinalPayableAmount,
    RegistrationStatus Status
);
```

### Use case interface

```csharp
public interface IRegistrationService
{
    Task<CreateRegistrationResponse> CreateRegistrationAsync(CreateRegistrationRequest request, CancellationToken ct);
}
```

### Discount engine interface (extendable)

```csharp
public record DiscountContext(
    Guid StudentId,
    Guid BatchId,
    Guid CourseId,
    decimal BaseFee
);

public interface IDiscountEngine
{
    Task<decimal> CalculateDiscountAmountAsync(string couponCode, DiscountContext ctx, CancellationToken ct);
}
```

---

## 4) Infrastructure layer (EF Core DbContext + configurations + constraints)

### DbContext

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Student email unique
        modelBuilder.Entity<Student>()
            .HasIndex(x => x.Email)
            .IsUnique();

        // Coupon PK
        modelBuilder.Entity<Coupon>()
            .HasKey(x => x.CouponCode);

        // ***Important: filtered unique index (one active registration per student)***
        modelBuilder.Entity<Registration>()
            .HasIndex(x => x.StudentId)
            .IsUnique()
            .HasFilter("[Status] IN (0, 1)"); // Reserved, Confirmed

        base.OnModelCreating(modelBuilder);
    }
}
```

Notes:

- The `.HasFilter(...)` works for **SQL Server**.
- This is the strongest way to enforce your “one active registration” rule, even under concurrency.

---

## 5) Registration service (core logic)

In `Application` you define the contract; in `Infrastructure` (or a separate “Application implementation” project) you implement it. A common approach:

- Interface in Application
- Implementation in Infrastructure (because it uses DbContext)

Pseudo-implementation (realistic and safe):

```csharp
public class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _db;
    private readonly IDiscountEngine _discountEngine;

    public RegistrationService(AppDbContext db, IDiscountEngine discountEngine)
    {
        _db = db;
        _discountEngine = discountEngine;
    }

    public async Task<CreateRegistrationResponse> CreateRegistrationAsync(CreateRegistrationRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        // 1) Upsert student by email
        var student = await _db.Students.SingleOrDefaultAsync(s => s.Email.ToLower() == email, ct);
        if (student is null)
        {
            student = new Student
            {
                StudentId = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Email = email,
                Phone = req.Phone?.Trim()
            };
            _db.Students.Add(student);
        }
        else
        {
            student.Name = req.Name.Trim();
            student.Phone = req.Phone?.Trim();
        }

        // 2) Load batch (also gets courseId)
        var batch = await _db.Batches.SingleAsync(b => b.BatchId == req.BatchId && b.IsActive, ct);

        // 3) Capacity check (active registrations in batch)
        var activeCount = await _db.Registrations.CountAsync(r =>
            r.BatchId == batch.BatchId &&
            (r.Status == RegistrationStatus.Reserved || r.Status == RegistrationStatus.Confirmed), ct);

        if (activeCount >= batch.Capacity)
            throw new InvalidOperationException("Batch is full.");

        // 4) Fee + discount
        var baseFee = batch.FeeAmount;
        decimal discountAmount = 0m;
        string? coupon = string.IsNullOrWhiteSpace(req.CouponCode) ? null : req.CouponCode.Trim().ToUpperInvariant();

        if (coupon is not null)
        {
            discountAmount = await _discountEngine.CalculateDiscountAmountAsync(
                coupon,
                new DiscountContext(student.StudentId, batch.BatchId, batch.CourseId, baseFee),
                ct
            );
        }

        var finalPayable = Math.Max(0m, baseFee - discountAmount);

        // 5) Create Registration
        var registration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            StudentId = student.StudentId,
            BatchId = batch.BatchId,
            CourseId = batch.CourseId,
            Status = RegistrationStatus.Reserved,
            BaseFee = baseFee,
            DiscountAmount = discountAmount,
            FinalPayableAmount = finalPayable,
            CouponCodeApplied = coupon
        };

        _db.Registrations.Add(registration);

        // 6) Save (DB filtered unique index enforces “one active registration per student”)
        await _db.SaveChangesAsync(ct);

        return new CreateRegistrationResponse(
            registration.RegistrationId,
            registration.BaseFee,
            registration.DiscountAmount,
            registration.FinalPayableAmount,
            registration.Status
        );
    }
}
```

In production, also catch `DbUpdateException` and translate it into a clean 409/400 response when the filtered unique index blocks a second active registration.

---

## 6) Migrations commands (EF Core)

From the **API** project (or whichever project hosts the startup):

```bash
dotnet ef migrations add InitialCreate \\
  --project Sunbeam.Registration.Infrastructure \\
  --startup-project Sunbeam.Registration.Api

dotnet ef database update \\
  --project Sunbeam.Registration.Infrastructure \\
  --startup-project Sunbeam.Registration.Api
```

---

## 7) API endpoints (minimum)

- `POST /api/registrations` (student registration)
- Admin:
    - `POST/PUT/DELETE /api/courses`
    - `POST/PUT/DELETE /api/batches`
    - `POST/PUT/DELETE /api/discounts`
    - `POST/PUT/DELETE /api/coupons`