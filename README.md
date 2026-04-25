# KgmApp - Khamgaon Gramastha Mandal Management System

A modern web application to help **Khamgaon Gramastha Mandal** manage members, meetings, contributions, and day-to-day committee operations in one place.

---

## Overview

`KgmApp` is a role-based organization management platform built for community administration.
It replaces manual records and scattered spreadsheets with a single, structured system for:

- member and sub-member records
- financial transaction tracking
- fee and contribution management
- meeting attendance monitoring
- report generation (PDF/JPG)
- communication features like announcements and suggestions

This project is designed to be useful for both:

- **Non-technical users** (committee members, admins, volunteers) through a simple dashboard and forms
- **Technical teams** through a clean ASP.NET Core architecture and PostgreSQL-backed data model

---

## Why This Application Exists

Many community groups struggle with:

- disconnected records across notebooks and Excel files
- unclear payment and pending dues status
- manual attendance tracking
- limited transparency in operations

`KgmApp` solves these issues by providing one trusted system where all important information is available, searchable, and report-ready.

---

## Key Features

### Members and Access

- 👤 Member and sub-member management (create, edit, search, import)
- 🔐 Mobile/password based login
- 🧭 Role-based navigation and rights
- 🔄 First-login password change flow
- ✅ Member active/inactive and login restriction support

### Meetings and Participation

- 📅 Meeting management
- 📝 Attendance tracking for general and committee meetings
- 📊 Attendance reports with filters and exports
- 🧑‍🤝‍🧑 Committee member listing and designation support

### Financial Management

- 💰 Income and expense transaction tracking
- 🏦 Cash and bank balance calculations
- 🧾 Contribution and registration fee tracking
- ⚙️ Configurable fee settings by effective date
- 📈 Statement reports for date ranges

### Data Operations and Reports

- 📥 Excel import for members and transactions
- 📄 PDF export for reports (QuestPDF)
- 🖼️ JPG export for selected reports
- 📢 Announcements, rules/regulations, and suggestion module
- 🧑‍💼 Login log tracking and audit visibility

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 8)
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core
- **UI:** Razor Views, Bootstrap, jQuery Validation
- **Reporting:** QuestPDF
- **Excel Processing:** ClosedXML
- **Image Processing:** SixLabors.ImageSharp

---

## Project Structure

```text
KgmApp/
|- Controllers/        # Request handling and business flow
|- Data/               # DbContext, seeders, DB helpers
|- Models/             # Domain models and view models
|- Reports/            # PDF/JPG report composers
|- Services/           # Application services
|- Views/              # Razor UI pages
|- wwwroot/            # Static assets (css/js/lib)
|- Program.cs          # App startup and middleware pipeline
```

---

## Getting Started

### Prerequisites

- [.NET SDK 8.0+](https://dotnet.microsoft.com/en-us/download)
- [PostgreSQL](https://www.postgresql.org/download/)
- Git

### 1) Clone the Repository

```bash
git clone <your-repository-url>
cd KgmApp
```

### 2) Configure Database Connection

Set one of the following:

- `ConnectionStrings:DefaultConnection` (in `appsettings.Development.json` or user secrets), or
- `DATABASE_URL` environment variable

Supported format examples:

```bash
# Npgsql style
Host=localhost;Port=5432;Database=kgmapp;Username=postgres;Password=yourpassword

# PostgreSQL URL style
postgresql://postgres:yourpassword@localhost:5432/kgmapp
```

### 3) Run the Application

From the project folder containing `KgmApp.csproj`:

```bash
dotnet restore
dotnet run
```

The app starts on local development URLs configured in `Properties/launchSettings.json` (for example `https://localhost:7231`).

### 4) Database Migrations

At startup, the app checks connectivity and applies pending EF Core migrations automatically when the database is reachable.

If you prefer manual migration commands:

```bash
dotnet ef database update
```

---

## Usage for Non-Technical Users

1. Open the web app URL in your browser.
2. Login with your registered mobile number and password.
3. Use the left menu to open modules (Members, Meetings, Transactions, Reports, etc.).
4. For first login, change password when prompted.
5. Use report screens to filter data and download PDF/JPG exports.

Tip: Keep member mobile numbers unique and accurate for smooth login and communication workflows.

---

## Configuration Notes

- `appsettings.json` contains base settings.
- `appsettings.Development.json` is recommended for local developer settings.
- `MemberUpiPayment` section is used to build payment links for pending dues.
- Session timeout is configured to 30 minutes in `Program.cs`.

---

## Security and Access

- Session-based authentication with role-aware routing
- Permission filters applied globally for menu access control
- Anti-forgery validation on POST forms
- Login restrictions for inactive or blocked accounts

---

## Deployment

This project can be deployed on any .NET 8 compatible environment (Windows/Linux/cloud) with PostgreSQL.

Basic production checklist:

- set production connection string / `DATABASE_URL`
- enable HTTPS and reverse-proxy forwarding headers
- store secrets securely (not in source control)
- monitor logs and database connectivity

---

## Contributing

Contributions are welcome.

If you are collaborating in a team:

1. create a feature branch
2. make focused changes
3. test locally
4. open a pull request with clear notes

---

## License

Add your preferred license here (for example MIT, Apache-2.0, or private/proprietary notice).

---

## Contact

For support, onboarding, or access setup, contact the Khamgaon Gramastha Mandal project maintainers.
