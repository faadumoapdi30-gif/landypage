# CarePoint Hospital Management System

CarePoint is an ASP.NET Core MVC student project with PostgreSQL-backed workflows for staff access, patients, doctors, appointments, medical records and billing. The default route opens the staff workspace; the academic design report remains available from the navigation menu.

## Requirements

- .NET 10 SDK
- PostgreSQL or Supabase PostgreSQL access
- Network access from the application host to the database

## Configure the database

Keep database credentials out of source control. For local development, set the connection string in .NET user-secrets from the repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:Supabase" "<your PostgreSQL connection string>" --project .\schoolapp\schoolapp.csproj
```

For hosted deployments, provide `ConnectionStrings__Supabase` through the hosting platform's secret manager. Use the connection method and SSL settings recommended for your Supabase project. Do not copy a service-role key into this setting; the application needs a database connection string.

Verify connectivity without modifying the database:

```powershell
dotnet run --project .\schoolapp\schoolapp.csproj -- --check-supabase
```

Review the generated migration, back up any existing database, then apply the schema:

```powershell
dotnet ef database update --project .\schoolapp\schoolapp.csproj
```

The migration adds HMS tables alongside the original sample `Students` table; it does not automatically run at application startup. Applying migrations changes the target database, so use a development database or obtain the database owner's approval first.

Run the PostgreSQL CRUD smoke test after applying the schema:

```powershell
dotnet run --project .\schoolapp\schoolapp.csproj -- --test-hms-crud
```

The test inserts, reads, searches, updates and deletes temporary patient, doctor, appointment, clinical-record, invoice, payment and staff records inside a database transaction, then rolls it back and verifies the temporary patient and account are absent. It does not create lasting test records.

## Run the application

```powershell
dotnet run --project .\schoolapp\schoolapp.csproj
```

On a new, migrated database, open the site and create the first administrator. First-time setup is available only while no staff account exists. The administrator can then add staff accounts and assign roles:

- **Admin:** staff and doctor administration, all modules.
- **Receptionist:** patient profiles and appointments.
- **Doctor:** assigned appointments and clinical records.
- **Billing:** invoices and payment entries.

All data-changing forms require an authenticated account and an antiforgery token. Passwords are stored as hashes. Patient and clinical data must not be entered until the institution has reviewed and approved the deployment.

## Important limitations

This is a student project, not a certified clinical system. Before any real use, add institutional privacy and clinical-safety review, multifactor authentication, account recovery and lockout policies, deployment hardening, monitoring, tested backup/restore procedures, approved retention rules, accessibility and security testing, and a configured currency/time zone. Do not use real patient information in an unapproved environment.
