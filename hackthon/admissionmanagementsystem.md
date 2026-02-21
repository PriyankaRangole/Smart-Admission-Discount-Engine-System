## Entities (C# classes)

### Student (Email = unique identifier)

```csharp
public class Student
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";   // unique
    public string Phone { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
```

### Course

```csharp
public class Course
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}
```

### Batch (under Course)

```csharp
public class Batch
{
    public Guid BatchId { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string BatchCode { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public decimal FeeAmount { get; set; }
    public int Capacity { get; set; }

    public string Mode { get; set; } = "";     // Online/Classroom/Hybrid
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
```

### Registration (1 active registration per student)

```csharp
public enum RegistrationStatus { Reserved, Confirmed, Cancelled, Completed }

public class Registration
{
    // Option A: GUID as RegistrationId
    public Guid RegistrationId { get; set; }

    // Option B: Separate human-friendly ID
    public string RegistrationCode { get; set; } = ""; // e.g. REG-2026-000001 (unique)

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public Guid CourseId { get; set; }  // store snapshot (optional but useful)
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Reserved;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // snapshot financials
    public decimal BaseFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPayableAmount { get; set; }

    public string? CouponCodeApplied { get; set; } // only one coupon
}
```

---

## Discount system (extendable)

### Discount definition + Coupon

```csharp
public enum DiscountType { Percent, Flat }

public class Discount
{
    public Guid DiscountId { get; set; }
    public string Name { get; set; } = "";
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }          // % or flat amount

    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinFeeAmount { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    // flexible rules later (allowed courses, modes, etc.)
    public string? MetadataJson { get; set; }   // JSON string
}

public class Coupon
{
    public string CouponCode { get; set; } = "";  // PK
    public Guid DiscountId { get; set; }
    public Discount Discount { get; set; } = null!;

    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerStudent { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### Coupon usage audit

```csharp
public class CouponUsage
{
    public Guid CouponUsageId { get; set; }
    public string CouponCode { get; set; } = "";
    public Guid RegistrationId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
```

---

## EF Core configuration (important constraints)

In `OnModelCreating`:

### 1) Unique Student Email

```csharp
modelBuilder.Entity<Student>()
    .HasIndex(s => s.Email)
    .IsUnique();
```

### 2) One active registration per student

This is best as a **filtered unique index** (SQL Server supports it, PostgreSQL supports partial index).

**SQL Server (EF Core supports HasFilter):**

```csharp
modelBuilder.Entity<Registration>()
    .HasIndex(r => r.StudentId)
    .IsUnique()
    .HasFilter("[Status] IN (0,1)"); // Reserved=0, Confirmed=1 depending on enum order
```

For PostgreSQL you typically create a migration with raw SQL:

```csharp
migrationBuilder.Sql(@"
CREATE UNIQUE INDEX ""uniq_active_registration_per_student""
ON ""Registrations"" (""StudentId"")
WHERE ""Status"" IN (0,1);
");
```

### 3) Unique RegistrationCode (if you use formatted IDs)

```csharp
modelBuilder.Entity<Registration>()
    .HasIndex(r => r.RegistrationCode)
    .IsUnique();
```

---

## Registration workflow (service method outline)

```csharp
public async Task<Registration> RegisterAsync(RegisterRequest req)
{
    // 1) Upsert student by email
    var student = await _db.Students.SingleOrDefaultAsync(s => s.Email == req.Email);
    if (student == null)
    {
        student = new Student { StudentId = Guid.NewGuid(), Email = req.Email, Name = req.Name, Phone = req.Phone };
        _db.Students.Add(student);
    }
    else
    {
        student.Name = req.Name;
        student.Phone = req.Phone;
    }

    // 2) Check active registration
    bool hasActive = await _db.Registrations.AnyAsync(r =>
        r.StudentId == student.StudentId &&
        (r.Status == RegistrationStatus.Reserved || r.Status == RegistrationStatus.Confirmed));

    if (hasActive) throw new Exception("Student already has an active registration.");

    // 3) Load batch + check capacity
    var batch = await _db.Batches.Include(b => b.Course).SingleAsync(b => b.BatchId == req.BatchId);
    int activeCount = await _db.Registrations.CountAsync(r =>
        r.BatchId == batch.BatchId &&
        (r.Status == RegistrationStatus.Reserved || r.Status == RegistrationStatus.Confirmed));

    if (activeCount >= batch.Capacity) throw new Exception("Batch is full.");

    // 4) Calculate fee + apply ONE coupon (optional)
    decimal baseFee = batch.FeeAmount;
    decimal discountAmount = 0m;
    string? appliedCoupon = null;

    if (!string.IsNullOrWhiteSpace(req.CouponCode))
    {
        // validate coupon + compute discountAmount
        // (check active, validity dates, usage limits, min fee, etc.)
        appliedCoupon = req.CouponCode.Trim().ToUpperInvariant();
        discountAmount = await _discountEngine.CalculateDiscountAsync(appliedCoupon, student.StudentId, baseFee, batch);
    }

    var finalPayable = Math.Max(0, baseFee - discountAmount);

    // 5) Create registration + generate ID
    var reg = new Registration
    {
        RegistrationId = Guid.NewGuid(),
        RegistrationCode = await _idService.NextRegistrationCodeAsync(), // if using formatted
        StudentId = student.StudentId,
        BatchId = batch.BatchId,
        CourseId = batch.CourseId,
        Status = RegistrationStatus.Reserved,
        BaseFee = baseFee,
        DiscountAmount = discountAmount,
        FinalPayableAmount = finalPayable,
        CouponCodeApplied = appliedCoupon
    };

    _db.Registrations.Add(reg);

    // 6) Save (unique index enforces rule even in race conditions)
    await _db.SaveChangesAsync();
    return reg;
}
```

---

### arget architecture ([ASP.NET](http://ASP.NET) Core Web API + SQL Server + React)

Build it as 3 layers:

1. **React UI**
    - Student registration form (Name, Email, Phone, Batch dropdown, Coupon)
    - Admin screens (Courses, Batches, Discounts, Coupons)
2. [**ASP.NET](http://ASP.NET) Core Web API**
    - Auth for Admin (JWT + [ASP.NET](http://ASP.NET) Identity)
    - Public registration endpoints for students
    - Business rules: one active registration, capacity check, discount calculation
3. **SQL Server**
    - Enforces uniqueness and “one active registration per student” with indexes

---

## SQL Server schema (tables + key constraints)

### Students

- Email must be unique (same student over time).

```sql
CREATE TABLE Students (
    StudentId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    Phone NVARCHAR(20) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_Students_Email ON Students(Email);
```

### Courses

```sql
CREATE TABLE Courses (
    CourseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CourseName NVARCHAR(150) NOT NULL,
    Description NVARCHAR(1000) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
```

### Batches

```sql
CREATE TABLE Batches (
    BatchId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CourseId UNIQUEIDENTIFIER NOT NULL,
    BatchCode NVARCHAR(50) NOT NULL,
    FeeAmount DECIMAL(18,2) NOT NULL,
    Capacity INT NOT NULL,
    Mode NVARCHAR(20) NOT NULL,      -- Online/Classroom/Hybrid
    Location NVARCHAR(200) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    StartDate DATE NULL,
    EndDate DATE NULL,
    CONSTRAINT FK_Batches_Courses FOREIGN KEY (CourseId) REFERENCES Courses(CourseId)
);
```

### Registrations (core rule: one active registration per student)

```sql
CREATE TABLE Registrations (
    RegistrationId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    RegistrationCode NVARCHAR(30) NOT NULL,  -- e.g. REG-2026-000001 (optional but recommended)
    StudentId UNIQUEIDENTIFIER NOT NULL,
    BatchId UNIQUEIDENTIFIER NOT NULL,
    CourseId UNIQUEIDENTIFIER NOT NULL,
    Status INT NOT NULL,                     -- 0 Reserved, 1 Confirmed, 2 Cancelled, 3 Completed
    RegisteredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    BaseFee DECIMAL(18,2) NOT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL,
    FinalPayableAmount DECIMAL(18,2) NOT NULL,
    CouponCodeApplied NVARCHAR(50) NULL,
    CONSTRAINT FK_Reg_Student FOREIGN KEY (StudentId) REFERENCES Students(StudentId),
    CONSTRAINT FK_Reg_Batch FOREIGN KEY (BatchId) REFERENCES Batches(BatchId)
);

CREATE UNIQUE INDEX UX_Registrations_Code ON Registrations(RegistrationCode);
```

**Filtered unique index (enforces one active registration per student):**

```sql
CREATE UNIQUE INDEX UX_ActiveReg_Student
ON Registrations(StudentId)
WHERE Status IN (0,1);  -- Reserved or Confirmed
```

### Discounts + Coupons (extendable)

```sql
CREATE TABLE Discounts (
    DiscountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    DiscountType INT NOT NULL,               -- 0 Percent, 1 Flat (extend later)
    Value DECIMAL(18,2) NOT NULL,
    MaxDiscountAmount DECIMAL(18,2) NULL,
    MinFeeAmount DECIMAL(18,2) NULL,
    ValidFrom DATETIME2 NULL,
    ValidTo DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    MetadataJson NVARCHAR(MAX) NULL          -- flexible rules
);

CREATE TABLE Coupons (
    CouponCode NVARCHAR(50) NOT NULL PRIMARY KEY,
    DiscountId UNIQUEIDENTIFIER NOT NULL,
    UsageLimitTotal INT NULL,
    UsageLimitPerStudent INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Coupon_Discount FOREIGN KEY (DiscountId) REFERENCES Discounts(DiscountId)
);

CREATE TABLE CouponUsages (
    CouponUsageId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CouponCode NVARCHAR(50) NOT NULL,
    RegistrationId UNIQUEIDENTIFIER NOT NULL,
    StudentId UNIQUEIDENTIFIER NOT NULL,
    UsedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

---

## RegistrationCode generation (best practice in SQL Server)

Use a sequence for human-friendly IDs:

```sql
CREATE SEQUENCE RegistrationNumberSeq
AS BIGINT
START WITH 1
INCREMENT BY 1;
```

In .NET:

- `next = SELECT NEXT VALUE FOR RegistrationNumberSeq`
- format: `REG-2026-000001`

---

## [ASP.NET](http://ASP.NET) Core Web API endpoints (minimal set)

### Public (Student)

- `GET /api/courses` (active courses)
- `GET /api/batches?courseId=...` (active batches + seats left)
- `POST /api/registrations`
    - Body: name, email, phone, batchId, couponCode(optional)
    - Returns: registrationCode, payable amounts

### Admin (JWT protected)

- `POST/PUT/DELETE /api/courses`
- `POST/PUT/DELETE /api/batches`
- `POST/PUT/DELETE /api/discounts`
- `POST/PUT/DELETE /api/coupons`

---

## Key business rules to implement in .NET service layer

When `POST /api/registrations`:

1. Upsert student by email (unique)
2. Check active registration for that student
3. Check batch capacity (count active regs in that batch)
4. Apply **one** coupon (validate, compute discount)
5. Store `BaseFee`, `DiscountAmount`, `FinalPayableAmount`
6. Create registration with generated `RegistrationCode`

The filtered unique index protects you even under concurrency (two clicks at once).

---

