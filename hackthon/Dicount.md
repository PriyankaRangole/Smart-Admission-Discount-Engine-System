### A clean, extensible Discount Engine design ([ASP.NET](http://ASP.NET) Core .NET 8 + EF Core + SQL Server)

The key is to model discounts as **config + eligibility rules**, and execute them via a **strategy/handler pipeline** so adding new discount types means adding a new handler, not editing registration code.

---

## 1) Data model (supports all your discount types)

### Core tables

#### `Discounts` (Admin creates these)

Each row is a “discount program”.

- `DiscountId` (Guid, PK)
    
- `Name`
    
- `DiscountKind` (int/enum)
    
    Examples: `EarlyBird`, `Loyalty`, `Individual`, `Combo`, `Group`, `Generic`
    
- `ValueType` (int/enum): `Flat` or `Percent`
    
- `Value` (decimal)
    
- `MaxDiscountAmount` (nullable)
    
- `ValidFromUtc`, `ValidToUtc` (nullable)
    
- `IsActive`
    
- `Priority` (int) _(optional; useful if later you auto-pick best discount)_
    
- `ConfigJson` (nvarchar(max)) _(stores type-specific settings, extendable without refactor)_
    

#### `Coupons`

Coupon is what the student enters. Coupon maps to exactly one discount program.

- `CouponCode` (PK string)
- `DiscountId` (FK)
- `IsActive`
- `UsageLimitTotal` (nullable)
- `UsageLimitPerStudent` (nullable)

#### `CouponUsages` (audit)

- `CouponUsageId` (Guid, PK)
- `CouponCode`
- `StudentId`
- `RegistrationId`
- `UsedAtUtc`

### Targeting / assignment (Admin can assign discounts dynamically)

Instead of hard-coding “student-specific” / “batch-specific”, model assignments:

#### `DiscountAssignments`

- `DiscountAssignmentId` (Guid, PK)
- `DiscountId` (FK)
- `TargetType` (enum): `Batch`, `Student`, `Course` _(keep minimal now)_
- `TargetId` (Guid) _(BatchId or StudentId or CourseId)_
- `ValidFromUtc`, `ValidToUtc` (nullable) _(time windows at assignment level too)_

This supports:

- “Specific batches” → assignment rows to BatchId
- “Specific students” → assignment rows to StudentId
- “Specific time windows” → assignment dates

For **Combo Batch Offer**, you can store the required batch list inside `Discount.ConfigJson`.

---

## 2) Discount engine (SOLID + no hard-coded rules in registration)

### Key interfaces (Application layer)

```csharp
public record DiscountContext(
    Guid StudentId,
    Guid BatchId,
    Guid CourseId,
    DateTime NowUtc,
    decimal BaseFee
);

public record DiscountResult(
    bool IsApplicable,
    decimal DiscountAmount,
    string Reason
);

public interface IDiscountHandler
{
    DiscountKind Kind { get; }
    Task<DiscountResult> EvaluateAsync(Discount discount, DiscountContext ctx, CancellationToken ct);
}
```

### Engine orchestration

Registration service only calls ONE thing:

```csharp
public interface IDiscountEngine
{
    Task<DiscountResult> ApplyCouponAsync(string couponCode, DiscountContext ctx, CancellationToken ct);
}
```

Implementation idea:

1. Load coupon + linked discount from DB
2. Validate global rules (active, date range, usage limits, “only one coupon”)
3. Resolve handler by `discount.DiscountKind`
4. Handler checks eligibility (early bird / loyalty / individual / combo / group)
5. If applicable → compute amount using `ValueType` + `Value` (plus caps)
6. Return discount amount to registration service

This keeps discount logic out of UI and out of the registration service.

---

## 3) Supported discount types (how they fit)

### A) Early Bird (date-based)

Eligibility:

- `ctx.NowUtc` is before some cutoff date, stored in `Discount.ConfigJson` (example: `EarlyBirdCutoffUtc`)
- Also validate discount validity window (`ValidFrom/ValidTo`) if you use both

### B) Loyalty (based on previous completed courses)

Eligibility:

- Count completed registrations for student:
    - `Registrations` where `StudentId = ctx.StudentId` and `Status = Completed`
- Rule thresholds come from `ConfigJson`, example:
    - `MinCompletedCourses = 1` or `MinCompletedCourses = 2`

### C) Individual discount (student-specific)

Eligibility:

- `DiscountAssignments` contains `(DiscountId, TargetType=Student, TargetId=ctx.StudentId)`
- Also validate assignment window dates if present

### D) Combo batch offer (predefined combinations)

Eligibility:

- `ConfigJson` contains required batches like `[batchId1, batchId2]`
- Check if student has completed/confirmed those batches (your rule: probably “Completed”)
- Apply discount on current registration if requirement met

### E) Flat / Percentage-based

This is not a separate kind. It is **ValueType + Value** used by all kinds.

### F) Group discount (bonus)

Eligibility depends on your definition. Two common approaches:

- **Same batch group booking** in one checkout: request contains `GroupSize`, discount applies if `GroupSize >= N`
- **Same batch active registrations count**: based on number of students registering together (less common unless you support group registration)

Store threshold in `ConfigJson`: `MinGroupSize`

---

## 4) “Only ONE discount per registration” enforcement

Do it at **API validation** + **DB storage**:

- In request DTO: accept only one `CouponCode`
- In `Registrations` table: `CouponCodeApplied` single column (not a list)
- If coupon is null, no discount applied

This is simple and audit-friendly.

---

## 5) Validation + edge cases (important)

Minimum checks:

- Coupon exists and is active
- Discount exists and is active
- Discount validity window (date checks)
- Usage limits:
    - total usages of coupon
    - per-student usages
- BaseFee must be > 0
- Discount amount:
    - never negative
    - never exceed BaseFee
    - apply `MaxDiscountAmount` if set
- Concurrency:
    - record coupon usage and registration in same transaction
    - rely on “one active registration per student” filtered unique index

---

## 6) What to build first (Phase 1 vs Phase 2)

**Phase 1 (must-have, fast, meets requirements)**

- Generic discount model + coupon
- Assignment targeting for Batch + Student
- EarlyBird + Individual discount handlers
- Flat/Percent computation + caps

**Phase 2**

- Loyalty handler
- Combo handler
- Group handler