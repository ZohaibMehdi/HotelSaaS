# Product Requirements Document (PRD)
# Multi-Tenant Hotel & Guest House Management SaaS

**Document Version:** 1.0  
**Status:** Product Baseline / Development Specification  
**Target Audience:** Product Owner, Business Analyst, Software Architects, Backend Developers, Frontend Developers, QA Engineers, DevOps Engineers, AI Coding Agents  
**Primary Stack:** ASP.NET Core Web API, Microsoft SQL Server, React.js + TypeScript  
**Architecture:** Multi-tenant SaaS  
**Primary Users:** SaaS Super Administrators, Hotel/Guest House Owners, Managers, Front Desk Staff, Account/Finance Staff, HR/Payroll Staff, Housekeeping Staff

---

## 1. Executive Summary

The product is a multi-tenant SaaS platform for hotels, guest houses, hostels, serviced residences, and similar accommodation businesses.

The platform allows a SaaS operator to onboard multiple accommodation businesses (tenants). Each tenant manages its own property information, rooms, guests, bookings, billing, employees, payroll, business customers, B2B agreements, and operational activities.

The system must provide strict tenant isolation while allowing the SaaS Super Administrator to manage the overall platform.

The product should be designed as a commercially deployable SaaS rather than as a single-property application.

---

# 2. Product Vision

Provide accommodation businesses with one centralized system to manage their daily operations from guest reservation through checkout, billing, employee management, and business agreements.

The system should:

- Reduce manual hotel/guest-house administration.
- Centralize operational information.
- Prevent duplicate or conflicting room bookings.
- Improve visibility of room occupancy and availability.
- Simplify guest billing.
- Support corporate/B2B customers and negotiated rates.
- Support employee and payroll administration.
- Provide management dashboards and reports.
- Allow the SaaS provider to onboard and manage multiple tenants.
- Maintain strong auditability and tenant-level data isolation.
- Be extensible to future mobile applications and third-party integrations.

---

# 3. Business Model

The platform is a SaaS product.

A **Tenant** represents an independent hotel, guest house, hostel, or accommodation business using the platform.

Example:

- Tenant A = Saibaan Guest House
- Tenant B = Hotel ABC
- Tenant C = Guest House XYZ

Tenant A must never see Tenant B's operational data.

The SaaS provider has a **Super Admin** role that can manage tenants and platform-level configuration.

The architecture must support many tenants without requiring a separate application deployment for every tenant.

---

# 4. Technology Requirements

## 4.1 Backend

Use:

- ASP.NET Core Web API
- C#
- Entity Framework Core may be used as an ORM for data access.
- Microsoft SQL Server
- JWT-based authentication
- Role-based authorization
- RESTful APIs
- Repository/Unit of Work may be used where justified, but do not introduce unnecessary abstraction.
- Swagger/OpenAPI
- FluentValidation or equivalent validation mechanism
- Structured logging
- Global exception handling
- Dependency Injection

### Important database requirement

The database should be designed deliberately using SQL/database design principles.

The system must not depend on EF Core Code First as the source of truth for business database design.

Preferred approach:

1. Design SQL Server schema.
2. Create tables, keys, indexes, constraints, views, stored procedures where genuinely required.
3. Create corresponding C# entity/model classes manually.
4. Configure EF Core mappings explicitly using Fluent API where appropriate.
5. Keep database schema and application model under controlled versioning.

EF Core is a data-access/mapping tool, not the business-domain designer.

---

## 4.2 Frontend

Use:

- React.js
- TypeScript
- Vite
- Tailwind CSS
- React Router
- Axios
- Context API or another lightweight state-management solution where required
- JWT authentication
- Responsive design

Do **not** use Create React App.

Do **not** use shadcn/ui unless explicitly requested later.

The UI should be reusable and component-based.

---

## 4.3 Database

Use:

- Microsoft SQL Server
- Proper primary and foreign keys
- Unique constraints
- Check constraints where useful
- Appropriate indexes
- Audit fields
- UTC timestamps in storage
- Tenant-aware indexing
- Soft deletion where business records must be retained

---

# 5. High-Level Architecture

Recommended architecture:

```text
React SPA
    |
    | HTTPS / JSON
    v
ASP.NET Core Web API
    |
    +-- Authentication / Authorization
    |
    +-- Tenant Resolution
    |
    +-- Application Services
    |
    +-- Domain/Business Rules
    |
    +-- Repositories / Data Access
    |
    v
Microsoft SQL Server
```

Optional infrastructure:

```text
React
  |
API
  |
SQL Server
  |
Background Worker / Scheduler
  |
Email/SMS/WhatsApp providers
```

---

# 6. Multi-Tenancy Requirements

Multi-tenancy is a core requirement.

## 6.1 Tenant

A Tenant is the accommodation business using the system.

Tenant fields should include, at minimum:

- TenantId
- TenantCode
- BusinessName
- LegalName
- Status
- ContactEmail
- ContactPhone
- Address
- City
- Country
- TimeZone
- Currency
- CreatedAt
- UpdatedAt
- IsActive

TenantCode must be unique.

---

## 6.2 Tenant Isolation

Every tenant-owned entity must contain TenantId directly or be safely related to a tenant-owned parent.

Examples:

- Rooms
- RoomTypes
- Guests
- Bookings
- BookingGuests
- Invoices
- Payments
- Employees
- Payroll records
- Corporate customers
- B2B agreements
- Company information

The API must derive tenant context from the authenticated user's claims/session wherever possible.

Do not trust a client-provided TenantId for authorization.

For example, a tenant user must not be able to modify:

```text
PUT /api/rooms/{roomId}
```

if the room belongs to another tenant, even if the user knows the room ID.

---

# 7. User Roles

Initial role model:

## 7.1 SuperAdmin

Platform-level administrator.

Can:

- Create tenants.
- Activate/deactivate tenants.
- View tenant list.
- View platform dashboard.
- Create tenant administrators.
- Manage platform-level users.
- View platform-level audit information.
- Configure system-level settings.

SuperAdmin should not automatically perform tenant operational actions unless explicitly authorized.

---

## 7.2 TenantAdmin / Owner

Can:

- Manage company information.
- Manage rooms and room types.
- Manage employees.
- Manage users.
- Manage guests.
- Manage bookings.
- Manage invoices.
- Manage payments.
- Manage B2B customers and agreements.
- View reports.
- Configure tenant settings.

---

## 7.3 Manager

Can manage day-to-day property operations.

---

## 7.4 FrontDesk

Can:

- Create reservations.
- Manage guests.
- Check availability.
- Check guests in/out.
- Create/modify guest charges.
- Generate invoices.
- Record payments.

Access should be restricted from sensitive administrative configuration.

---

## 7.5 Finance/Accounts

Can:

- View invoices.
- Record payments.
- Manage financial reports.
- Manage billing-related configuration.

---

## 7.6 HR/Payroll

Can:

- Manage employees.
- Manage employee salaries.
- Manage attendance where enabled.
- Process payroll.
- View payroll reports.

---

## 7.7 Housekeeping

Can:

- View assigned rooms.
- Update housekeeping status.
- View room operational status.

Should not access financial or sensitive guest information unnecessarily.

---

# 8. Authentication

Implement JWT authentication.

Login flow:

```text
User enters email/username + password
        |
        v
API validates credentials
        |
        v
Generate JWT
        |
        +-- UserId
        +-- Role
        +-- TenantId
        +-- Expiration
        |
        v
React stores authentication state
```

The JWT should contain claims required for authorization, including:

- Subject/UserId
- Role
- TenantId where applicable

Passwords must never be stored in plaintext.

Use a secure password hashing algorithm.

Implement:

- Login
- Logout/client token invalidation strategy
- Password change
- Password reset
- Account activation/deactivation
- Failed login handling
- Authorization middleware

---

# 9. Core Functional Modules

The application should contain these modules:

1. Platform Administration
2. Tenant Management
3. User & Role Management
4. Company Information
5. Property Configuration
6. Room Type Management
7. Room Management
8. Guest Management
9. Reservation/Booking Management
10. Check-in / Check-out
11. Guest Charges
12. Billing & Invoicing
13. Payments
14. B2B/Corporate Customer Management
15. B2B Agreement Management
16. Employee Management
17. Payroll
18. Housekeeping
19. Dashboard & Reporting
20. Notifications
21. Audit Logging
22. System Settings

---

# 10. Tenant Onboarding

SuperAdmin should have an onboarding workflow.

## 10.1 Create Tenant

Fields:

- Business name
- Legal name
- Contact person
- Email
- Phone
- Address
- City
- Country
- Currency
- Time zone
- Tenant code
- Status

After tenant creation:

```text
Create Tenant
      |
      v
Create Tenant Admin
      |
      v
Send/Display initial credentials securely
      |
      v
Tenant Admin logs in
      |
      v
Complete company/property setup
```

The onboarding process should be extensible.

---

# 11. Company Information

Each tenant can maintain company/property information.

Fields:

- CompanyInformationId
- TenantId
- BusinessName
- DisplayName
- LegalName
- RegistrationNumber
- TaxNumber
- Address
- City
- Country
- PostalCode
- Phone
- Email
- Website
- Logo
- InvoiceHeader
- InvoiceFooter
- Tax settings
- CreatedAt
- UpdatedAt

Only authorized tenant users can edit it.

---

# 12. Property Configuration

A tenant should be able to configure:

- Property name
- Property type
- Number of floors
- Check-in time
- Check-out time
- Currency
- Time zone
- Tax configuration
- Invoice numbering
- Booking policies
- Cancellation policies
- Payment policies

---

# 13. Room Type Management

Room types define categories.

Example:

```text
Standard
Deluxe
Executive
Suite
Family
```

Fields:

- RoomTypeId
- TenantId
- Name
- Description
- MaximumOccupancy
- BasePrice
- ExtraAdultPrice
- ExtraChildPrice
- NumberOfBeds
- BedConfiguration
- Amenities
- IsActive

A tenant can create multiple room types.

---

# 14. Room Management

Each physical room is an individual inventory unit.

Example:

```text
Room 101
Room 102
Room 103
```

Fields:

- RoomId
- TenantId
- RoomTypeId
- RoomNumber
- Floor
- Status
- Notes
- IsActive

Room statuses:

- Available
- Reserved
- Occupied
- Cleaning
- Maintenance
- OutOfService

Room number must be unique within a tenant.

---

# 15. Room Availability

The system must determine room availability based on:

- Existing reservations
- Check-in date
- Check-out date
- Room status
- Maintenance blocks
- Cancellation status

A room must not be double-booked.

The backend must enforce booking conflict validation.

Frontend availability checks are not sufficient.

---

# 16. Guest Management

Guest fields:

- GuestId
- TenantId
- FirstName
- LastName
- Gender where legally/business appropriate
- DateOfBirth where required
- Nationality where required
- CNIC/Passport/ID type
- ID number
- Phone
- Email
- Address
- City
- Country
- Notes
- CreatedAt
- UpdatedAt

Sensitive information should be protected appropriately.

Guests may have booking history.

A guest should be reusable across multiple bookings within the same tenant.

---

# 17. Accompanying Guests

A primary guest may have accompanying guests.

Example:

```text
Booking
 |
 +-- Primary Guest
 |
 +-- Accompany Guest 1
 +-- Accompany Guest 2
```

Store:

- Name
- ID information where required
- Relationship
- Contact information where appropriate

---

# 18. Reservation Management

A reservation represents a planned stay.

Fields should include:

- BookingId
- TenantId
- BookingNumber
- PrimaryGuestId
- BookingSource
- BookingStatus
- CheckInDate
- CheckOutDate
- NumberOfAdults
- NumberOfChildren
- RoomId or room allocation details
- Rate
- Discount
- Tax
- TotalAmount
- Deposit
- SpecialRequests
- Notes
- CreatedBy
- CreatedAt
- UpdatedAt

Booking sources may include:

- Walk-in
- Phone
- Website
- Corporate
- Travel Agent
- OTA
- Other

Statuses:

- Pending
- Confirmed
- CheckedIn
- CheckedOut
- Cancelled
- NoShow

---

# 19. Booking Workflow

```text
Search Availability
       |
       v
Select Room
       |
       v
Enter Guest
       |
       v
Calculate Rate
       |
       v
Apply Discount/Agreement
       |
       v
Create Reservation
       |
       v
Confirm
       |
       v
Check-in
       |
       v
Stay
       |
       v
Add Charges
       |
       v
Check-out
       |
       v
Generate Final Invoice
       |
       v
Payment / Outstanding Balance
```

---

# 20. Check-In

At check-in:

- Validate reservation.
- Validate guest information.
- Confirm assigned room.
- Record actual arrival.
- Change booking status to CheckedIn.
- Change room status to Occupied.
- Record user performing check-in.
- Support walk-in check-in if no reservation exists.

System must prevent check-in if the room is unavailable or invalid.

---

# 21. Check-Out

At checkout:

- Calculate outstanding balance.
- Add final charges.
- Record checkout date/time.
- Generate/finalize invoice.
- Record payment.
- Change booking status to CheckedOut.
- Change room status to Cleaning.

After housekeeping completes cleaning, room can become Available.

---

# 22. Guest Billing

Charges may include:

- Room charges
- Food
- Laundry
- Extra bed
- Additional guest
- Transport
- Other services
- Manual adjustments

Each charge should contain:

- ChargeId
- BookingId
- Description
- Quantity
- UnitPrice
- Discount
- Tax
- Total
- CreatedBy
- CreatedAt

---

# 23. Food Billing

The system should support food/restaurant charges against a guest stay.

Example:

```text
Booking #B-1001

Room: 5,000
Breakfast: 500
Lunch: 1,000
Laundry: 300

Subtotal: 6,800
Tax: ...
Total: ...
```

Food billing should be configurable because some properties may include breakfast in room rates.

---

# 24. Invoice Management

Invoices should have:

- InvoiceId
- TenantId
- InvoiceNumber
- BookingId
- GuestId
- InvoiceDate
- Subtotal
- Discount
- Tax
- Total
- PaidAmount
- Balance
- Status

Invoice statuses:

- Draft
- Issued
- PartiallyPaid
- Paid
- Cancelled

Invoice numbers must be unique per tenant.

Invoices should be printable/PDF-ready.

---

# 25. Payments

Support:

- Cash
- Bank Transfer
- Card
- Online Payment
- Other

Payment fields:

- PaymentId
- TenantId
- InvoiceId/BookingId
- Amount
- PaymentMethod
- TransactionReference
- PaymentDate
- Notes
- ReceivedBy

The system must prevent payment amounts from creating incorrect balances unless explicitly allowing overpayments.

---

# 26. B2B / Corporate Customer Management

This is a major feature.

Businesses may have agreements with a hotel.

Example:

```text
Company: ABC Corporation

Agreement:
Standard Room = PKR 5,000
Deluxe Room = PKR 7,000
Breakfast = Included
Payment Terms = 30 Days
```

Corporate customer fields:

- CorporateCustomerId
- TenantId
- CompanyName
- RegistrationNumber
- TaxNumber
- ContactPerson
- Phone
- Email
- Address
- BillingAddress
- Status

---

# 27. B2B Agreements

Agreement fields:

- AgreementId
- TenantId
- CorporateCustomerId
- AgreementNumber
- StartDate
- EndDate
- Status
- PaymentTerms
- CreditLimit
- Notes

Agreement rate details:

- RoomType
- ContractedRate
- Meal inclusion
- Extra guest pricing
- Applicable taxes
- Special facilities
- Cancellation rules

Agreement status:

- Draft
- Active
- Expired
- Suspended
- Cancelled

---

# 28. Corporate Booking

When creating a booking:

```text
Booking Type = Corporate
       |
       v
Select Corporate Customer
       |
       v
Select Active Agreement
       |
       v
Select Room
       |
       v
System applies contracted rate
```

The user should not have to manually calculate the corporate rate.

The backend must validate that the agreement is active and applicable to the booking dates.

---

# 29. Employee Management

Employee fields:

- EmployeeId
- TenantId
- EmployeeCode
- FirstName
- LastName
- CNIC/Employee ID where required
- Phone
- Email
- Address
- Department
- Designation
- JoiningDate
- EmploymentStatus
- Salary
- Bank information where required
- Emergency contact where appropriate

Departments:

- Front Desk
- Housekeeping
- Accounts
- Kitchen
- Management
- Maintenance
- Security
- Other

---

# 30. Payroll

Payroll should support:

- Basic salary
- Allowances
- Overtime
- Bonuses
- Deductions
- Advances
- Loans
- Absences
- Net salary

Payroll calculation:

```text
Gross Salary
+ Allowances
+ Overtime
+ Bonuses
- Deductions
- Advances
= Net Salary
```

Payroll records should be immutable after finalization except through controlled adjustment/reversal processes.

---

# 31. Housekeeping

Housekeeping staff can see rooms requiring action.

Statuses:

- Dirty
- Cleaning
- Clean
- Inspected
- Maintenance

After checkout:

```text
Occupied
   |
   v
Dirty
   |
   v
Cleaning
   |
   v
Clean
   |
   v
Available
```

Optional future workflow:

```text
Clean
   |
   v
Inspection
   |
   v
Approved
```

---

# 32. Dashboard

Tenant dashboard should show:

- Total rooms
- Available rooms
- Occupied rooms
- Reserved rooms
- Rooms under maintenance
- Today's arrivals
- Today's departures
- Current guests
- Today's revenue
- Outstanding balances
- Pending bookings
- Occupancy percentage

Example:

```text
Rooms
12 Total

Available       5
Occupied        5
Reserved        1
Maintenance     1
```

---

# 33. SuperAdmin Dashboard

Platform dashboard should show:

- Total tenants
- Active tenants
- Inactive tenants
- Total users
- Active users
- Total rooms across tenants
- Total bookings
- Revenue metrics if the SaaS platform stores such information
- Recent tenant onboarding
- Recent activity
- System health indicators

SuperAdmin dashboard must not expose tenant financial/guest data unnecessarily.

---

# 34. Reports

Tenant reports should include:

### Occupancy

- Daily occupancy
- Monthly occupancy
- Occupancy percentage
- Room utilization

### Revenue

- Daily revenue
- Monthly revenue
- Revenue by room type
- Revenue by service
- Outstanding payments

### Booking

- Booking source
- Cancellation report
- No-show report
- Booking history

### Guest

- Guest history
- Frequent guests
- Guest statistics

### B2B

- Corporate bookings
- Agreement utilization
- Corporate receivables
- Revenue by company

### Payroll

- Monthly payroll
- Department payroll
- Employee salary summary

---

# 35. Notifications

Notification framework should support:

- In-app notifications
- Email
- SMS/WhatsApp in future

Events:

- Booking confirmation
- Booking cancellation
- Check-in reminder
- Payment receipt
- Outstanding balance
- Agreement expiration
- Payroll completion
- Password reset

Notifications should be abstracted behind interfaces so providers can be changed.

---

# 36. Audit Logging

Audit logging is required for important actions.

Record:

- User
- Tenant
- Action
- Entity
- EntityId
- Timestamp
- Old value where appropriate
- New value where appropriate
- IP address where appropriate
- Correlation/request ID

Examples:

```text
User Ahmed changed Room 101 status
User Ali created Booking B-1001
User Sara issued Invoice INV-1022
User Admin changed Company Information
```

Audit logs should be append-oriented and protected from ordinary users.

---

# 37. API Requirements

Use RESTful APIs.

Example:

```text
/api/auth/login

/api/tenants
/api/users

/api/company-information

/api/room-types
/api/rooms

/api/guests
/api/bookings

/api/bookings/{id}/check-in
/api/bookings/{id}/check-out

/api/charges
/api/invoices
/api/payments

/api/corporate-customers
/api/b2b-agreements

/api/employees
/api/payroll

/api/housekeeping

/api/dashboard
/api/reports
```

Use HTTP status codes correctly:

- 200 OK
- 201 Created
- 204 No Content
- 400 Bad Request
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict
- 422 Unprocessable Entity where appropriate
- 500 Internal Server Error

---

# 38. DTO Requirements

Do not expose EF/database entities directly from controllers.

Use DTOs.

Example:

```text
CreateRoomDto
UpdateRoomDto
RoomDto
```

Similarly:

```text
CreateGuestDto
UpdateGuestDto
GuestDto
```

DTOs should control:

- Input validation
- Output shape
- Sensitive-field exposure
- API version compatibility

---

# 39. Backend Layering

Recommended structure:

```text
src/
  HotelSaaS.Api/
  HotelSaaS.Application/
  HotelSaaS.Domain/
  HotelSaaS.Infrastructure/
```

### API

Controllers, middleware, authentication configuration.

### Application

Use cases, DTOs, validators, interfaces, application services.

### Domain

Entities, domain rules, enums, value objects where useful.

### Infrastructure

EF Core, SQL Server, repositories, external integrations.

Avoid creating abstractions purely for architectural appearance.

---

# 40. Database Design Principles

Every table should be deliberately designed.

Common audit columns:

```text
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
IsActive
```

Use soft delete only where appropriate.

Transactional records such as invoices, payments, bookings, and payroll should generally not be physically deleted.

Instead use status/cancellation/reversal concepts.

---

# 41. Important Database Entities

Initial database should include at least:

```text
Tenants
Users
Roles
UserRoles

CompanyInformations

RoomTypes
Rooms

Guests
AccompanyGuests

Bookings
BookingRooms
BookingGuests

Charges
FoodBills

Invoices
InvoiceItems
Payments

CorporateCustomers
B2BAgreements
B2BAgreementRates

Employees
Departments
PayrollRuns
PayrollItems

HousekeepingTasks

Notifications

AuditLogs
```

The exact normalization may be adjusted during technical design.

---

# 42. Booking Data Model Consideration

Do not assume a booking can only ever contain one room.

The data model should support:

```text
Booking
   |
   +-- Room 101
   +-- Room 102
```

This allows future support for families/groups and corporate bookings.

Use a booking-room relationship where appropriate.

---

# 43. Concurrency

Concurrency is critical for room booking.

Two users may attempt to book the same room simultaneously.

The backend must prevent double booking using:

- Transactional validation
- Appropriate database constraints/locking strategy
- Optimistic concurrency where appropriate
- Rechecking availability immediately before commit

Do not rely solely on frontend availability.

---

# 44. Security Requirements

Implement:

- HTTPS
- Secure password hashing
- JWT authentication
- Role authorization
- Tenant authorization
- Input validation
- SQL injection protection
- XSS-safe output handling
- CORS configuration
- Rate limiting where appropriate
- Secure secrets management
- No passwords/tokens in logs
- Audit logging
- Proper error handling

Never return raw exception details in production.

---

# 45. Frontend Application Structure

Recommended:

```text
src/
  api/
  components/
  contexts/
  hooks/
  layouts/
  pages/
  routes/
  services/
  types/
  utils/
```

Layouts:

```text
AuthLayout
DashboardLayout
```

Pages:

```text
Login
Dashboard
Tenants
TenantDetails
OnboardTenant

CompanyInformation
RoomTypes
Rooms

Guests
Bookings
CheckIn
CheckOut

Invoices
Payments

CorporateCustomers
B2BAgreements

Employees
Payroll
Housekeeping

Reports
Settings
```

---

# 46. Frontend Authentication

React should:

- Login through API.
- Store authentication state securely.
- Decode JWT only for UI purposes.
- Use Axios interceptors for authenticated API requests.
- Redirect unauthenticated users.
- Handle 401/403 responses.
- Hide unauthorized navigation items.
- Never rely on frontend role checks as the security boundary.

Backend authorization is authoritative.

---

# 47. Navigation

SuperAdmin:

```text
Dashboard
Tenants
Users
System Settings
Audit Logs
```

Tenant Admin:

```text
Dashboard
Bookings
Guests
Rooms
Room Types
Companies
B2B Agreements
Billing
Employees
Payroll
Housekeeping
Reports
Settings
```

Front Desk:

```text
Dashboard
Bookings
Guests
Check-in
Check-out
Billing
```

Housekeeping:

```text
Dashboard
Rooms
Housekeeping
```

Menu visibility should be role-aware.

---

# 48. UI/UX Requirements

The UI should be:

- Professional
- Clean
- Responsive
- Desktop-first for hotel administration
- Tablet-friendly
- Mobile-friendly for operational users
- Consistent
- Accessible

Use reusable components:

- Buttons
- Modal
- Table
- Pagination
- Search
- Filters
- Form fields
- Selects
- Date pickers
- Status badges
- Cards
- Alerts
- Toast notifications
- Confirmation dialogs

---

# 49. Search and Filtering

Large datasets must support server-side:

- Pagination
- Search
- Sorting
- Filtering

Examples:

```text
GET /api/guests?page=1&pageSize=20&search=Ahmed
```

Do not load thousands of records into React unnecessarily.

---

# 50. Validation

Validation must exist at both:

### Frontend

For usability.

### Backend

For security and correctness.

Backend validation is authoritative.

Examples:

- Check-out must be after check-in.
- Room must belong to tenant.
- Agreement must be active.
- Invoice total must be valid.
- Payment cannot exceed allowed balance unless explicitly supported.
- Required guest identity fields must be present where business rules require them.

---

# 51. Error Handling

API should return a consistent error structure.

Example:

```json
{
  "success": false,
  "message": "Room is already booked for the selected dates.",
  "errors": []
}
```

Validation:

```json
{
  "success": false,
  "message": "Validation failed.",
  "errors": {
    "checkOutDate": [
      "Check-out date must be after check-in date."
    ]
  }
}
```

---

# 52. Logging

Use structured logging.

Log:

- Request ID
- User ID
- Tenant ID
- Endpoint
- HTTP method
- Status code
- Duration
- Exception information

Do not log:

- Passwords
- JWTs
- Sensitive identity documents
- Payment secrets

---

# 53. Configuration

Use environment-specific configuration.

Examples:

```text
Development
Staging
Production
```

Configuration should include:

- SQL Server connection
- JWT settings
- Allowed CORS origins
- Email provider
- Storage provider
- Application URL
- Logging settings

Secrets should not be committed to source control.

---

# 54. File Storage

Future support should exist for:

- Company logo
- Guest documents
- Employee documents
- Invoice PDFs
- Agreement documents

Do not store large files directly in SQL Server unless there is a clear reason.

Prefer object/file storage with database metadata.

---

# 55. Future Mobile Application

The backend must be API-first so that a future mobile application can consume the same APIs.

Potential mobile apps:

### Hotel Owner/Manager

Dashboard, bookings, revenue.

### Front Desk

Reservations, check-in/out, guest information.

### Housekeeping

Room status and task updates.

The API must not be coupled to React-specific behavior.

---

# 56. Future Integrations

Architecture should leave room for:

- Online booking website
- OTA integrations
- Payment gateways
- SMS
- WhatsApp
- Email
- Accounting systems
- Government reporting
- Biometric attendance
- POS systems
- Mobile applications

Do not implement all of these in version 1 unless specifically prioritized.

---

# 57. MVP Scope

The first production MVP should prioritize:

1. Authentication
2. Multi-tenancy
3. SuperAdmin tenant management
4. Tenant users
5. Company information
6. Room types
7. Rooms
8. Guests
9. Bookings
10. Availability
11. Check-in
12. Check-out
13. Charges
14. Invoices
15. Payments
16. Basic dashboard
17. B2B customers
18. B2B agreements
19. Basic employee management
20. Audit logging

Payroll, advanced reports, notifications, integrations, and mobile applications can be delivered in later phases if schedule requires.

---

# 58. Non-Functional Requirements

## Performance

Typical API operations should respond within an acceptable business application threshold under normal load.

Avoid unnecessary database calls.

Use pagination and indexed queries.

## Scalability

The architecture should support increasing:

- Tenants
- Users
- Rooms
- Bookings
- Transactions

without redesigning the entire application.

## Availability

Production deployment should support appropriate backups and recovery.

## Maintainability

Code must be:

- Modular
- Testable
- Documented
- Consistent
- Free of unnecessary duplication

## Security

Tenant data isolation is mandatory.

---

# 59. Testing Requirements

Implement:

### Unit Tests

For:

- Pricing calculations
- Booking rules
- Availability
- B2B agreement pricing
- Invoice calculations
- Payroll calculations

### Integration Tests

For:

- Authentication
- Authorization
- Tenant isolation
- Booking creation
- Check-in
- Check-out
- Billing

### Frontend Tests

For:

- Login
- Route protection
- Forms
- Booking workflow
- Role-based UI

### Critical Security Tests

Verify:

```text
Tenant A user cannot read Tenant B data.
Tenant A user cannot modify Tenant B data.
Tenant A user cannot delete Tenant B data.
Tenant A cannot access SuperAdmin endpoints.
Front Desk cannot access payroll administration.
```

---

# 60. Acceptance Criteria

## Tenant isolation

Given a user belongs to Tenant A,

when the user requests Tenant B's room,

then the API must deny access.

## Room booking

Given Room 101 is booked from 10-Aug to 12-Aug,

when another user attempts to book Room 101 for 11-Aug to 13-Aug,

then the API must reject the booking.

## Check-in

Given a confirmed booking exists,

when an authorized front-desk user checks the guest in,

then:

- Booking becomes CheckedIn.
- Room becomes Occupied.
- Check-in timestamp is recorded.
- User is recorded.

## Check-out

Given an occupied booking,

when checkout is completed,

then:

- Charges are finalized.
- Invoice is generated/finalized.
- Payment/balance is calculated.
- Booking becomes CheckedOut.
- Room becomes Cleaning.

## B2B

Given an active corporate agreement,

when a corporate booking is created,

then the applicable contracted rate must be applied automatically.

---

# 61. Development Rules for AI Coding Agent

The AI developer must follow these rules.

## Rule 1 — Do not invent requirements

If something is ambiguous, identify it as an assumption or ask for clarification.

## Rule 2 — Build incrementally

Do not generate the entire application as one unstructured code dump.

Implement module-by-module.

Recommended order:

```text
1. Database foundation
2. Backend architecture
3. Authentication
4. Multi-tenancy
5. SuperAdmin
6. Tenant/company
7. Room management
8. Guest management
9. Booking
10. Check-in/out
11. Billing
12. B2B
13. Employees
14. Payroll
15. Housekeeping
16. Reports
17. Audit
18. Hardening
```

## Rule 3 — Database first

The SQL Server schema must be intentionally designed.

Provide:

- Table definitions
- Keys
- Foreign keys
- Indexes
- Constraints
- Seed/reference data where appropriate

Then map the schema into C#.

## Rule 4 — Never bypass tenant authorization

Every tenant-owned query must be tenant-scoped.

## Rule 5 — Never trust client TenantId

Resolve tenant from authenticated identity.

## Rule 6 — Never expose EF entities directly

Use DTOs.

## Rule 7 — Business rules belong on the server

The frontend is not a security or business-rule boundary.

## Rule 8 — Do not over-engineer

Use clean architecture principles where they improve maintainability, but avoid unnecessary frameworks and abstractions.

## Rule 9 — Preserve existing functionality

When modifying a module, do not break unrelated modules.

## Rule 10 — Explain architectural decisions

For significant decisions, document why the implementation was chosen.

---

# 62. Recommended Development Milestones

## Milestone 1 — Foundation

Deliver:

- SQL database
- API solution
- React solution
- Configuration
- Logging
- Exception handling
- Swagger
- Basic deployment structure

## Milestone 2 — Authentication & RBAC

Deliver:

- Users
- Roles
- Login
- JWT
- Authorization
- Tenant context

## Milestone 3 — Tenant Administration

Deliver:

- Tenant CRUD
- Onboarding
- Company information
- Tenant users

## Milestone 4 — Property Management

Deliver:

- Room types
- Rooms
- Amenities
- Room status

## Milestone 5 — Guest & Booking

Deliver:

- Guests
- Accompany guests
- Availability
- Reservations
- Booking status

## Milestone 6 — Stay Operations

Deliver:

- Check-in
- Check-out
- Room lifecycle
- Guest charges

## Milestone 7 — Billing

Deliver:

- Invoices
- Invoice items
- Payments
- Receivables

## Milestone 8 — B2B

Deliver:

- Corporate customers
- Agreements
- Contract rates
- Corporate bookings

## Milestone 9 — Workforce

Deliver:

- Employees
- Departments
- Payroll
- Housekeeping

## Milestone 10 — Reporting & Hardening

Deliver:

- Reports
- Audit logs
- Performance optimization
- Security testing
- Automated tests
- Production readiness

---

# 63. Definition of Done

A feature is complete only when:

- Database changes are implemented.
- API endpoint/service is implemented.
- Authorization is implemented.
- Tenant isolation is verified.
- DTOs are implemented.
- Validation is implemented.
- Frontend UI is implemented.
- Loading/error states exist.
- Relevant tests exist.
- Swagger/API documentation is updated.
- Audit requirements are addressed.
- No known regression has been introduced.

---

# 64. Important Business Rules

1. A tenant can only access its own data.
2. SuperAdmin manages platform-level tenants.
3. Room numbers are unique within a tenant.
4. A room cannot be double-booked.
5. Cancelled bookings do not block room availability unless business rules explicitly say otherwise.
6. Checked-in bookings cannot be casually deleted.
7. Financial records should be reversed/cancelled rather than physically deleted.
8. Invoice numbers are unique per tenant.
9. Payment records must be traceable to their source.
10. B2B rates must be validated against active agreements.
11. Expired agreements cannot automatically apply contractual rates.
12. Payroll finalized records require controlled correction.
13. Important operational and financial actions must be auditable.
14. Frontend authorization must never replace backend authorization.
15. Every tenant-owned database query must respect tenant scope.

---

# 65. Example End-to-End Scenario

## Walk-in Guest

A guest arrives.

Front Desk:

1. Searches guest.
2. Creates guest if not found.
3. Checks available rooms.
4. Selects room.
5. Creates booking.
6. Performs check-in.
7. System marks room Occupied.
8. Guest orders food.
9. Food charge is added.
10. Guest requests laundry.
11. Laundry charge is added.
12. Guest checks out.
13. System calculates final invoice.
14. Guest pays.
15. System records payment.
16. Invoice becomes Paid.
17. Booking becomes CheckedOut.
18. Room becomes Cleaning.
19. Housekeeping marks room Clean.
20. Room becomes Available.

---

# 66. Example B2B Scenario

ABC Corporation has an agreement with the hotel.

Agreement:

```text
Standard = PKR 5,000
Deluxe = PKR 7,000
Breakfast = Included
Payment = 30 Days
```

ABC sends a guest.

Front Desk:

1. Selects Corporate Booking.
2. Selects ABC Corporation.
3. Selects active agreement.
4. Enters guest information.
5. Selects Standard room.
6. System applies PKR 5,000 contracted rate.
7. Guest checks in.
8. Guest checks out.
9. Invoice is generated against ABC.
10. Payment remains outstanding.
11. Accounts later records payment.

This should be a first-class workflow, not a manual discount workaround.

---

# 67. Example Multi-Tenant Scenario

```text
SuperAdmin
   |
   +-- Tenant A: Saibaan Guest House
   |       |
   |       +-- Rooms
   |       +-- Guests
   |       +-- Bookings
   |       +-- Employees
   |
   +-- Tenant B: Hotel ABC
           |
           +-- Rooms
           +-- Guests
           +-- Bookings
           +-- Employees
```

Tenant A must never receive Tenant B records through any API.

---

# 68. Data Ownership Matrix

| Entity | Tenant Owned | SuperAdmin | TenantAdmin | FrontDesk | Finance | HR | Housekeeping |
|---|---:|---:|---:|---:|---:|---:|---:|
| Tenant | No | Full | View | No | No | No | No |
| Company Information | Yes | Platform access | Full | View | View | No | No |
| Rooms | Yes | Platform access | Full | Manage | View | No | Update status |
| Guests | Yes | Platform access | Full | Full | Limited | No | No |
| Bookings | Yes | Platform access | Full | Full | View | No | Limited |
| Invoices | Yes | Platform access | Full | Create/View | Full | No | No |
| Payments | Yes | Platform access | Full | Limited | Full | No | No |
| B2B Agreements | Yes | Platform access | Full | View | View | No | No |
| Employees | Yes | Platform access | Full | Limited | View | Full | No |
| Payroll | Yes | Platform access | Full | No | View | Full | No |
| Housekeeping | Yes | Platform access | Full | View | No | No | Full |
| Audit Logs | Yes | Platform | View | Limited | Limited | Limited | Limited |

This matrix should be refined into concrete permissions before implementation.

---

# 69. Future SaaS Commercial Features

These are outside the initial operational MVP but should be architecturally possible:

- Subscription plans
- Trial periods
- Monthly/yearly billing
- Feature limits
- Tenant subscription status
- Online SaaS payments
- Usage metering
- Plan-based permissions
- Tenant suspension for unpaid subscription
- Platform revenue dashboard

Potential plans:

```text
Starter
Professional
Enterprise
```

Do not implement subscription billing unless it is explicitly included in the project scope.

---

# 70. Final Product Goal

The completed product should allow a hotel or guest house owner to run the majority of day-to-day operations from one application:

```text
                         HOTEL SaaS

                              |
        +---------------------+----------------------+
        |                     |                      |
     Operations            Finance                 HR
        |                     |                      |
   Reservations           Invoices                Employees
   Guests                  Payments                Payroll
   Rooms                   Receivables
   Check-in/out
   Housekeeping
        |
        +---------------------+
                              |
                           B2B
                              |
                    Corporate Customers
                    Agreements
                    Contract Rates
```

The platform must be:

- Multi-tenant
- Secure
- Scalable
- API-first
- Database-driven
- Maintainable
- Mobile-ready
- Audit-friendly
- Suitable for real-world hotel/guest-house operations

The first implementation should prioritize correctness, tenant isolation, booking integrity, financial accuracy, and maintainability over visual complexity.

---

# 71. Instructions to the Implementing AI Agent

You are acting as the senior software architect and engineering team for this product.

Before writing production code:

1. Analyze this PRD.
2. Identify ambiguities and assumptions.
3. Propose the database ERD.
4. Propose the SQL Server schema.
5. Propose API architecture.
6. Propose React application architecture.
7. Propose RBAC/permission design.
8. Identify risks.
9. Break the implementation into milestones.
10. Implement one milestone at a time.

For every milestone:

- Show the planned changes.
- Create/update SQL schema.
- Create/update C# backend.
- Create/update React frontend.
- Implement validation.
- Implement authorization.
- Implement tenant isolation.
- Add tests.
- Explain how to run it.
- Verify that existing functionality is not broken.

Do not generate fake integrations or pretend that an unimplemented feature works.

When a requirement is ambiguous, explicitly label the assumption.

The application must be designed as a serious production-oriented multi-tenant SaaS platform rather than a tutorial/demo application.

---

# 72. Product Success Criteria

The product will be considered successful when:

- Multiple independent properties can operate on the same platform.
- Tenant data is securely isolated.
- SuperAdmin can onboard and manage tenants.
- Tenant administrators can configure their properties.
- Front desk can manage reservations and stays.
- Rooms cannot be double-booked.
- Guests can be managed centrally.
- Check-in/check-out works reliably.
- Charges and invoices are accurate.
- Payments and outstanding balances are traceable.
- Corporate agreements automatically apply negotiated rates.
- Employees and payroll can be managed.
- Housekeeping can maintain room status.
- Managers can see meaningful operational dashboards.
- Important actions are auditable.
- The backend can later serve a mobile application.
- The system can scale beyond a single property without redesigning its core architecture.

---

## End of Product Requirements Document
