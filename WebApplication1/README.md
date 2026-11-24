# Student CRUD (ASP.NET Core MVC)

A simple Student CRUD built on the default ASP.NET Core MVC template in this
folder. Data currently lives in memory, so it resets on every restart. The
storage layer is behind `IStudentRepository`, ready to be swapped for Supabase.

## Run

```powershell
dotnet run --project WebApplication1
```

Open the URL printed in the console, then click **Students** in the navbar (or
go to `/Students`).

## What is included

| Item | Path |
| --- | --- |
| Student model + validation | `WebApplication1/Models/Student.cs` |
| Repository abstraction | `WebApplication1/Data/IStudentRepository.cs` |
| In-memory implementation + seed data | `WebApplication1/Data/InMemoryStudentRepository.cs` |
| CRUD controller | `WebApplication1/Controllers/StudentsController.cs` |
| Views (list, details, create, edit, delete) | `WebApplication1/Views/Students/` |

Operations: list + search, create, view details, edit, delete, duplicate-email
validation, server-side validation, and anti-forgery tokens on POSTs.

## Connecting Supabase later

1. Create the table by running `supabase/students.sql` in the Supabase SQL
   editor. The demo RLS policies are for local development only.
2. Fill in `Supabase` settings in `WebApplication1/appsettings.json` (keep the
   service-role key out of source control; use user-secrets or environment
   variables):

   ```json
   "Supabase": {
     "Url": "https://YOUR-PROJECT.supabase.co",
     "AnonKey": "YOUR-ANON-KEY",
     "StudentsTable": "students"
   }
   ```

3. Add a Supabase-backed repository. Either option works:

   **Option A - Supabase C# client**

   ```powershell
   dotnet add WebApplication1 package Supabase
   ```

   Create `Data/SupabaseStudentRepository.cs` implementing `IStudentRepository`
   and using `Supabase.Client` (`From<Student>()` with column mappings matching
   the SQL above). Register the client with
   `builder.Services.AddSingleton(await new Client(url, key).InitializeAsync())`.

   **Option B - EF Core + Npgsql**

   ```powershell
   dotnet add WebApplication1 package Npgsql.EntityFrameworkCore.PostgreSQL
   ```

   Use the Supabase connection string from **Project Settings -> Database** and
   implement the repository over a `DbContext`.

4. Replace the registration in `Program.cs`:

   ```csharp
   builder.Services.AddSingleton<IStudentRepository, InMemoryStudentRepository>();
   ```

   with your Supabase repository registration. No controller or view changes
   are required.

> Note: the SQL table uses `bigint` ids, which map cleanly to the model's `int`
> `Id` for small data sets. If you expect ids above `int.MaxValue`, change
> `Student.Id` to `long` (and the delete/update route constraints follow
> automatically).
