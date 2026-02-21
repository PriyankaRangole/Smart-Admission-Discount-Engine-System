# Sprint 1 – Task 1: Create Solution Structure

You will now create:

Sunbeam.Admission.sln  
 ├── Sunbeam.Admission.Domain        (Class Library)  
 ├── Sunbeam.Admission.Application   (Class Library)  
 ├── Sunbeam.Admission.Infrastructure (Class Library)  
 └── Sunbeam.Admission.Api           (Web API)

---

# 🛠 If You Are Using Visual Studio

1. Create **Blank Solution**
    
2. Add 3 Class Library projects (.NET 8)
    
3. Add 1 ASP.NET Core Web API project (.NET 8)
    
4. Name them exactly as above
    

---

# 🔗 Now Add Project References (VERY IMPORTANT)

Right-click → Add Reference:

### Api project references:

- Application
    
- Infrastructure
    

### Infrastructure references:

- Application
    
- Domain
    

### Application references:

- Domain
    

Domain → No reference

---

# 📦 NuGet Packages (Infrastructure + API)

Install in **Infrastructure**:

Microsoft.EntityFrameworkCore  
Microsoft.EntityFrameworkCore.SqlServer  
Microsoft.EntityFrameworkCore.Design

Install in **API**:
