/* ============================================================================
   HotelSaaS_DB - Multi-Tenant Hotel & Guest House Management SaaS
   ----------------------------------------------------------------------------
   DATABASE-FIRST SCHEMA (source of truth). EF Core maps to this schema; it is
   never used to generate business tables.

   Conventions
     * All timestamps: datetime2, stored in UTC (sysutcdatetime()).
     * Audit columns: CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsActive.
       (CreatedBy/UpdatedBy hold UserId values; intentionally NOT FK-constrained
        to avoid circular references Tenants <-> Users.)
     * Soft delete: IsActive = 0 on master data. Transactional records
       (bookings, invoices, payments, payroll) are never physically deleted;
       status/reversal semantics are used instead.
     * Enum-like columns are tinyint/int with documented value ranges and CHECK
       constraints where valuable.
     * Tenant-owned entities carry TenantId with tenant-scoped indexes.
   ============================================================================ */

IF DB_ID(N'HotelSaaS_DB') IS NULL
BEGIN
    CREATE DATABASE HotelSaaS_DB;
END;
GO

USE HotelSaaS_DB;
GO
-- Required SET options for filtered indexes / computed column indexes
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/* Safe re-run: drop child-most objects first */
DROP VIEW IF EXISTS dbo.vw_FoodBills;
DROP TABLE IF EXISTS dbo.AuditLogs;
DROP TABLE IF EXISTS dbo.Notifications;
DROP TABLE IF EXISTS dbo.HousekeepingTasks;
DROP TABLE IF EXISTS dbo.PayrollItems;
DROP TABLE IF EXISTS dbo.PayrollRuns;
DROP TABLE IF EXISTS dbo.Payments;
DROP TABLE IF EXISTS dbo.InvoiceItems;
DROP TABLE IF EXISTS dbo.Invoices;
DROP TABLE IF EXISTS dbo.Charges;
DROP TABLE IF EXISTS dbo.AccompanyGuests;
DROP TABLE IF EXISTS dbo.BookingGuests;
DROP TABLE IF EXISTS dbo.BookingRooms;
DROP TABLE IF EXISTS dbo.Bookings;
DROP TABLE IF EXISTS dbo.B2BAgreementRates;
DROP TABLE IF EXISTS dbo.B2BAgreements;
DROP TABLE IF EXISTS dbo.CorporateCustomers;
DROP TABLE IF EXISTS dbo.Guests;
DROP TABLE IF EXISTS dbo.Employees;
DROP TABLE IF EXISTS dbo.Departments;
DROP TABLE IF EXISTS dbo.Rooms;
DROP TABLE IF EXISTS dbo.RoomTypes;
DROP TABLE IF EXISTS dbo.PropertySettings;
DROP TABLE IF EXISTS dbo.CompanyInformations;
DROP TABLE IF EXISTS dbo.RefreshTokens;
DROP TABLE IF EXISTS dbo.UserRoles;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.Roles;
DROP TABLE IF EXISTS dbo.Tenants;
DROP TABLE IF EXISTS dbo.SystemSettings;
GO

/* ============================== PLATFORM ================================= */

CREATE TABLE dbo.Tenants
(
    TenantId      bigint IDENTITY(1,1) NOT NULL,
    TenantCode    varchar(30)          NOT NULL,
    BusinessName  nvarchar(150)        NOT NULL,
    LegalName     nvarchar(150)        NULL,
    Status        tinyint              NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT(0), -- 0 Pending,1 Active,2 Suspended,3 Inactive
    ContactEmail  nvarchar(200)        NOT NULL,
    ContactPhone  nvarchar(30)         NULL,
    Address       nvarchar(300)        NULL,
    City          nvarchar(80)         NULL,
    Country       nvarchar(80)         NULL,
    TimeZone      nvarchar(100)        NOT NULL CONSTRAINT DF_Tenants_TimeZone DEFAULT(N'UTC'),
    Currency      char(3)              NOT NULL CONSTRAINT DF_Tenants_Currency DEFAULT('PKR'),
    CreatedAt     datetime2(0)         NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy     bigint               NULL,
    UpdatedAt     datetime2(0)         NULL,
    UpdatedBy     bigint               NULL,
    IsActive      bit                  NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT(1),
    CONSTRAINT PK_Tenants PRIMARY KEY CLUSTERED (TenantId),
    CONSTRAINT CK_Tenants_Status CHECK (Status IN (0,1,2,3))
);
CREATE UNIQUE INDEX UX_Tenants_TenantCode ON dbo.Tenants(TenantCode);
GO

-- Fixed reference roles. Ids are stable and seeded explicitly.
CREATE TABLE dbo.Roles
(
    RoleId tinyint       NOT NULL,
    Name   varchar(30)   NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId)
);
GO

CREATE TABLE dbo.Users
(
    UserId             bigint IDENTITY(1,1) NOT NULL,
    TenantId           bigint               NULL, -- NULL => platform (SuperAdmin) user
    FullName           nvarchar(150)        NOT NULL,
    Email              nvarchar(200)        NOT NULL,
    Phone              nvarchar(30)         NULL,
    PasswordHash       nvarchar(500)        NULL, -- PBKDF2-SHA256; NULL only until first-run seeder assigns default password
    MustChangePassword bit                  NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0),
    ResetTokenHash     nvarchar(500)        NULL,
    ResetTokenExpiresAt datetime2(0)        NULL,
    FailedLoginCount   smallint             NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT(0),
    LockedUntil        datetime2(0)         NULL,
    LastLoginAt        datetime2(0)         NULL,
    CreatedAt          datetime2(0)         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy          bigint               NULL,
    UpdatedAt          datetime2(0)         NULL,
    UpdatedBy          bigint               NULL,
    IsActive           bit                  NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT(1),
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_Users_Email_NotEmpty CHECK (LEN(LTRIM(RTRIM(Email))) > 3)
);
CREATE UNIQUE INDEX UX_Users_PlatformEmail ON dbo.Users(Email) WHERE TenantId IS NULL;
CREATE UNIQUE INDEX UX_Users_TenantEmail  ON dbo.Users(TenantId, Email) WHERE TenantId IS NOT NULL AND IsActive = 1;
CREATE INDEX IX_Users_Tenant ON dbo.Users(TenantId) WHERE TenantId IS NOT NULL;
GO

CREATE TABLE dbo.UserRoles
(
    UserId bigint  NOT NULL,
    RoleId tinyint NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);
GO

CREATE TABLE dbo.RefreshTokens
(
    TokenId     bigint IDENTITY(1,1) NOT NULL,
    UserId      bigint               NOT NULL,
    TokenHash   nvarchar(500)        NOT NULL, -- SHA-256 of the opaque token; raw token never stored
    ExpiresAt   datetime2(0)         NOT NULL,
    RevokedAt   datetime2(0)         NULL,
    ReplacedByToken bigint            NULL,
    CreatedAt   datetime2(0)         NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT(sysutcdatetime()),
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (TokenId),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON dbo.RefreshTokens(TokenHash);
CREATE INDEX IX_RefreshTokens_UserId ON dbo.RefreshTokens(UserId);
GO

CREATE TABLE dbo.SystemSettings
(
    SettingId    int IDENTITY(1,1) NOT NULL,
    SettingKey   nvarchar(100)     NOT NULL,
    SettingValue nvarchar(max)     NULL,
    Description  nvarchar(200)     NULL,
    UpdatedAt    datetime2(0)      NULL,
    UpdatedBy    bigint            NULL,
    CONSTRAINT PK_SystemSettings PRIMARY KEY CLUSTERED (SettingId)
);
CREATE UNIQUE INDEX UX_SystemSettings_Key ON dbo.SystemSettings(SettingKey);
GO

/* ========================== TENANT CONFIGURATION ========================= */

CREATE TABLE dbo.CompanyInformations
(
    CompanyInformationId bigint IDENTITY(1,1) NOT NULL,
    TenantId             bigint               NOT NULL,
    BusinessName         nvarchar(150)        NOT NULL,
    DisplayName          nvarchar(150)        NULL,
    LegalName            nvarchar(150)        NULL,
    RegistrationNumber   nvarchar(60)         NULL,
    TaxNumber            nvarchar(60)         NULL,
    Address              nvarchar(300)        NULL,
    City                 nvarchar(80)         NULL,
    Country              nvarchar(80)         NULL,
    PostalCode           nvarchar(20)         NULL,
    Phone                nvarchar(30)         NULL,
    Email                nvarchar(200)        NULL,
    Website              nvarchar(200)        NULL,
    LogoPath             nvarchar(400)        NULL,
    InvoiceHeader        nvarchar(300)        NULL,
    InvoiceFooter        nvarchar(500)        NULL,
    TaxPercentage        decimal(5,2)         NOT NULL CONSTRAINT DF_CompanyInfo_TaxPct DEFAULT(0),
    TaxEnabled           bit                  NOT NULL CONSTRAINT DF_CompanyInfo_TaxEnabled DEFAULT(0),
    CreatedAt            datetime2(0)         NOT NULL CONSTRAINT DF_CompanyInfo_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy            bigint               NULL,
    UpdatedAt            datetime2(0)         NULL,
    UpdatedBy            bigint               NULL,
    IsActive             bit                  NOT NULL CONSTRAINT DF_CompanyInfo_IsActive DEFAULT(1),
    CONSTRAINT PK_CompanyInformations PRIMARY KEY CLUSTERED (CompanyInformationId),
    CONSTRAINT FK_CompanyInfo_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId)
);
CREATE UNIQUE INDEX UX_CompanyInfo_Tenant ON dbo.CompanyInformations(TenantId);
GO

CREATE TABLE dbo.PropertySettings
(
    PropertySettingsId   bigint IDENTITY(1,1) NOT NULL,
    TenantId             bigint               NOT NULL,
    PropertyName         nvarchar(150)        NOT NULL,
    PropertyType         nvarchar(50)         NOT NULL CONSTRAINT DF_PropSet_Type DEFAULT(N'GuestHouse'), -- Hotel/GuestHouse/Hostel/ServicedResidence/Other
    NumberOfFloors       int                  NULL,
    CheckInTime          time(0)              NOT NULL CONSTRAINT DF_PropSet_CheckInTime DEFAULT('14:00'),
    CheckOutTime         time(0)              NOT NULL CONSTRAINT DF_PropSet_CheckOutTime DEFAULT('12:00'),
    CurrencyCode         char(3)              NOT NULL CONSTRAINT DF_PropSet_Currency DEFAULT('PKR'),
    TimeZoneId           nvarchar(100)        NOT NULL CONSTRAINT DF_PropSet_TZ DEFAULT(N'UTC'),
    TaxRatePercent       decimal(5,2)         NOT NULL CONSTRAINT DF_PropSet_TaxRate DEFAULT(0),
    TaxInclusive         bit                  NOT NULL CONSTRAINT DF_PropSet_TaxInclusive DEFAULT(0), -- rates include tax when 1
    InvoicePrefix        nvarchar(10)         NOT NULL CONSTRAINT DF_PropSet_InvPrefix DEFAULT(N'INV-'),
    InvoiceCounter       int                  NOT NULL CONSTRAINT DF_PropSet_InvCounter DEFAULT(0),
    BookingPrefix        nvarchar(10)         NOT NULL CONSTRAINT DF_PropSet_BkPrefix DEFAULT(N'BK-'),
    BookingCounter       int                  NOT NULL CONSTRAINT DF_PropSet_BkCounter DEFAULT(1000000),
    DefaultBreakfastIncluded bit              NOT NULL CONSTRAINT DF_PropSet_Breakfast DEFAULT(0),
    BookingPolicyText    nvarchar(1000)       NULL,
    CancellationPolicyText nvarchar(1000)     NULL,
    PaymentPolicyText    nvarchar(1000)       NULL,
    CreatedAt            datetime2(0)         NOT NULL CONSTRAINT DF_PropSet_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy            bigint               NULL,
    UpdatedAt            datetime2(0)         NULL,
    UpdatedBy            bigint               NULL,
    IsActive             bit                  NOT NULL CONSTRAINT DF_PropSet_IsActive DEFAULT(1),
    CONSTRAINT PK_PropertySettings PRIMARY KEY CLUSTERED (PropertySettingsId),
    CONSTRAINT FK_PropertySettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_PropSet_TaxRate CHECK (TaxRatePercent BETWEEN 0 AND 100)
);
CREATE UNIQUE INDEX UX_PropertySettings_Tenant ON dbo.PropertySettings(TenantId);
GO

/* ============================ ROOM INVENTORY ============================= */

CREATE TABLE dbo.RoomTypes
(
    RoomTypeId      bigint IDENTITY(1,1) NOT NULL,
    TenantId        bigint               NOT NULL,
    Name            nvarchar(100)        NOT NULL,
    Description     nvarchar(500)        NULL,
    MaxOccupancy    int                  NOT NULL CONSTRAINT DF_RoomTypes_MaxOcc DEFAULT(2),
    BasePrice       decimal(18,2)        NOT NULL,
    ExtraAdultPrice decimal(18,2)        NOT NULL CONSTRAINT DF_RoomTypes_ExtraAdult DEFAULT(0),
    ExtraChildPrice decimal(18,2)        NOT NULL CONSTRAINT DF_RoomTypes_ExtraChild DEFAULT(0),
    NumberOfBeds    int                  NOT NULL CONSTRAINT DF_RoomTypes_Beds DEFAULT(1),
    BedConfiguration nvarchar(100)       NULL,
    Amenities       nvarchar(max)        NULL, -- comma-separated list; normalized catalogue is a future enhancement
    SortOrder       int                  NOT NULL CONSTRAINT DF_RoomTypes_SortOrder DEFAULT(0),
    CreatedAt       datetime2(0)         NOT NULL CONSTRAINT DF_RoomTypes_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy       bigint               NULL,
    UpdatedAt       datetime2(0)         NULL,
    UpdatedBy       bigint               NULL,
    IsActive        bit                  NOT NULL CONSTRAINT DF_RoomTypes_IsActive DEFAULT(1),
    CONSTRAINT PK_RoomTypes PRIMARY KEY CLUSTERED (RoomTypeId),
    CONSTRAINT FK_RoomTypes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_RoomTypes_BasePrice CHECK (BasePrice >= 0),
    CONSTRAINT CK_RoomTypes_MaxOcc CHECK (MaxOccupancy BETWEEN 1 AND 20)
);
CREATE UNIQUE INDEX UX_RoomTypes_Tenant_Name ON dbo.RoomTypes(TenantId, Name) WHERE IsActive = 1;
GO

CREATE TABLE dbo.Rooms
(
    RoomId             bigint IDENTITY(1,1) NOT NULL,
    TenantId           bigint               NOT NULL,
    RoomTypeId         bigint               NOT NULL,
    RoomNumber         nvarchar(20)         NOT NULL,
    Floor              int                  NULL,
    Status             tinyint              NOT NULL CONSTRAINT DF_Rooms_Status DEFAULT(0), -- 0 Available,1 Reserved,2 Occupied,3 Cleaning,4 Maintenance,5 OutOfService
    HousekeepingStatus tinyint              NOT NULL CONSTRAINT DF_Rooms_HkStatus DEFAULT(0), -- 0 Clean,1 Dirty,2 Cleaning,3 Inspected,4 Maintenance
    Notes              nvarchar(500)        NULL,
    CreatedAt          datetime2(0)         NOT NULL CONSTRAINT DF_Rooms_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy          bigint               NULL,
    UpdatedAt          datetime2(0)         NULL,
    UpdatedBy          bigint               NULL,
    IsActive           bit                  NOT NULL CONSTRAINT DF_Rooms_IsActive DEFAULT(1),
    CONSTRAINT PK_Rooms PRIMARY KEY CLUSTERED (RoomId),
    CONSTRAINT FK_Rooms_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_Rooms_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES dbo.RoomTypes(RoomTypeId),
    CONSTRAINT CK_Rooms_Status CHECK (Status IN (0,1,2,3,4,5)),
    CONSTRAINT CK_Rooms_HkStatus CHECK (HousekeepingStatus IN (0,1,2,3,4))
);
CREATE UNIQUE INDEX UX_Rooms_Tenant_Number ON dbo.Rooms(TenantId, RoomNumber) WHERE IsActive = 1;
CREATE INDEX IX_Rooms_Tenant_Status ON dbo.Rooms(TenantId, Status) INCLUDE(RoomTypeId, RoomNumber);
GO

/* ================================ WORKFORCE =============================== */

CREATE TABLE dbo.Departments
(
    DepartmentId bigint IDENTITY(1,1) NOT NULL,
    TenantId     bigint               NOT NULL,
    Name         nvarchar(100)        NOT NULL,
    CreatedAt    datetime2(0)         NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT(sysutcdatetime()),
    IsActive     bit                  NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT(1),
    CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (DepartmentId),
    CONSTRAINT FK_Departments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId)
);
CREATE UNIQUE INDEX UX_Departments_Tenant_Name ON dbo.Departments(TenantId, Name) WHERE IsActive = 1;
GO

CREATE TABLE dbo.Employees
(
    EmployeeId             bigint IDENTITY(1,1) NOT NULL,
    TenantId               bigint               NOT NULL,
    EmployeeCode           nvarchar(30)         NOT NULL,
    FirstName              nvarchar(80)         NOT NULL,
    LastName               nvarchar(80)         NULL,
    CNIC                   nvarchar(50)         NULL,
    Phone                  nvarchar(30)         NULL,
    Email                  nvarchar(200)        NULL,
    Address                nvarchar(300)        NULL,
    DepartmentId           bigint               NULL,
    Designation            nvarchar(100)        NULL,
    JoiningDate            date                 NOT NULL,
    EmploymentStatus       tinyint              NOT NULL CONSTRAINT DF_Employees_EmpStatus DEFAULT(1), -- 0 Probation,1 Active,2 OnLeave,3 Suspended,4 Terminated
    BasicSalary            decimal(18,2)        NOT NULL CONSTRAINT DF_Employees_BasicSalary DEFAULT(0),
    BankName               nvarchar(120)        NULL,
    BankAccountNumber      nvarchar(60)         NULL,
    EmergencyContactName   nvarchar(120)        NULL,
    EmergencyContactPhone  nvarchar(30)         NULL,
    TerminationDate        date                 NULL,
    CreatedAt              datetime2(0)         NOT NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy              bigint               NULL,
    UpdatedAt              datetime2(0)         NULL,
    UpdatedBy              bigint               NULL,
    IsActive               bit                  NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT(1),
    CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (EmployeeId),
    CONSTRAINT FK_Employees_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId),
    CONSTRAINT CK_Employees_Salary CHECK (BasicSalary >= 0)
);
CREATE UNIQUE INDEX UX_Employees_Tenant_Code ON dbo.Employees(TenantId, EmployeeCode) WHERE IsActive = 1;
CREATE INDEX IX_Employees_Tenant_Dept ON dbo.Employees(TenantId, DepartmentId);
GO

/* ================================= GUESTS ================================= */

CREATE TABLE dbo.Guests
(
    GuestId     bigint IDENTITY(1,1) NOT NULL,
    TenantId    bigint               NOT NULL,
    FirstName   nvarchar(80)         NOT NULL,
    LastName    nvarchar(80)         NULL,
    Gender      tinyint              NULL, -- 0 Male,1 Female,2 Other
    DateOfBirth date                 NULL,
    Nationality nvarchar(80)         NULL,
    IdType      tinyint              NOT NULL CONSTRAINT DF_Guests_IdType DEFAULT(0), -- 0 CNIC,1 Passport,2 NationalID,3 DrivingLicense,4 Other
    IdNumber    nvarchar(60)         NULL,
    Phone       nvarchar(30)         NULL,
    Email       nvarchar(200)        NULL,
    Address     nvarchar(300)        NULL,
    City        nvarchar(80)         NULL,
    Country     nvarchar(80)         NULL,
    Notes       nvarchar(500)        NULL,
    CreatedAt   datetime2(0)         NOT NULL CONSTRAINT DF_Guests_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy   bigint               NULL,
    UpdatedAt   datetime2(0)         NULL,
    UpdatedBy   bigint               NULL,
    IsActive    bit                  NOT NULL CONSTRAINT DF_Guests_IsActive DEFAULT(1),
    CONSTRAINT PK_Guests PRIMARY KEY CLUSTERED (GuestId),
    CONSTRAINT FK_Guests_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_Guests_Gender CHECK (Gender IS NULL OR Gender IN (0,1,2)),
    CONSTRAINT CK_Guests_IdType CHECK (IdType IN (0,1,2,3,4))
);
CREATE INDEX IX_Guests_Tenant_LastName ON dbo.Guests(TenantId, LastName, FirstName);
CREATE INDEX IX_Guests_Tenant_Phone ON dbo.Guests(TenantId, Phone);
CREATE INDEX IX_Guests_Tenant_IdNumber ON dbo.Guests(TenantId, IdNumber);
GO

/* ============================== B2B / CORPORATE =========================== */

CREATE TABLE dbo.CorporateCustomers
(
    CorporateCustomerId bigint IDENTITY(1,1) NOT NULL,
    TenantId            bigint               NOT NULL,
    CompanyName         nvarchar(150)        NOT NULL,
    RegistrationNumber  nvarchar(60)         NULL,
    TaxNumber           nvarchar(60)         NULL,
    ContactPerson       nvarchar(120)        NULL,
    Phone               nvarchar(30)         NULL,
    Email               nvarchar(200)        NULL,
    Address             nvarchar(300)        NULL,
    BillingAddress      nvarchar(300)        NULL,
    Status              tinyint              NOT NULL CONSTRAINT DF_CorpCust_Status DEFAULT(1), -- 0 Inactive,1 Active,2 Suspended
    Notes               nvarchar(500)        NULL,
    CreatedAt           datetime2(0)         NOT NULL CONSTRAINT DF_CorpCust_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy           bigint               NULL,
    UpdatedAt           datetime2(0)         NULL,
    UpdatedBy           bigint               NULL,
    IsActive            bit                  NOT NULL CONSTRAINT DF_CorpCust_IsActive DEFAULT(1),
    CONSTRAINT PK_CorporateCustomers PRIMARY KEY CLUSTERED (CorporateCustomerId),
    CONSTRAINT FK_CorporateCustomers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId)
);
CREATE UNIQUE INDEX UX_CorpCust_Tenant_Name ON dbo.CorporateCustomers(TenantId, CompanyName) WHERE IsActive = 1;
GO

CREATE TABLE dbo.B2BAgreements
(
    AgreementId        bigint IDENTITY(1,1) NOT NULL,
    TenantId           bigint               NOT NULL,
    CorporateCustomerId bigint              NOT NULL,
    AgreementNumber    nvarchar(40)         NOT NULL,
    StartDate          date                 NOT NULL,
    EndDate            date                 NOT NULL,
    Status             tinyint              NOT NULL CONSTRAINT DF_Agr_Status DEFAULT(0), -- 0 Draft,1 Active,2 Expired,3 Suspended,4 Cancelled
    PaymentTermsDays   int                  NOT NULL CONSTRAINT DF_Agr_PayTerms DEFAULT(30),
    CreditLimit        decimal(18,2)        NULL,
    IncludesBreakfast  bit                  NOT NULL CONSTRAINT DF_Agr_Breakfast DEFAULT(0),
    SpecialFacilities  nvarchar(500)        NULL,
    CancellationRules  nvarchar(500)        NULL,
    Notes              nvarchar(500)        NULL,
    CreatedAt          datetime2(0)         NOT NULL CONSTRAINT DF_Agr_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy          bigint               NULL,
    UpdatedAt          datetime2(0)         NULL,
    UpdatedBy          bigint               NULL,
    IsActive           bit                  NOT NULL CONSTRAINT DF_Agr_IsActive DEFAULT(1),
    CONSTRAINT PK_B2BAgreements PRIMARY KEY CLUSTERED (AgreementId),
    CONSTRAINT FK_B2BAgreements_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_B2BAgreements_CorpCust FOREIGN KEY (CorporateCustomerId) REFERENCES dbo.CorporateCustomers(CorporateCustomerId),
    CONSTRAINT CK_Agr_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_Agr_Status CHECK (Status IN (0,1,2,3,4))
);
CREATE UNIQUE INDEX UX_Agr_Tenant_Number ON dbo.B2BAgreements(TenantId, AgreementNumber) WHERE IsActive = 1;
CREATE INDEX IX_Agr_Tenant_Corp ON dbo.B2BAgreements(TenantId, CorporateCustomerId);
GO

CREATE TABLE dbo.B2BAgreementRates
(
    AgreementRateId bigint IDENTITY(1,1) NOT NULL,
    AgreementId     bigint               NOT NULL,
    TenantId        bigint               NOT NULL,
    RoomTypeId      bigint               NOT NULL,
    ContractedRate  decimal(18,2)        NOT NULL,
    ExtraAdultRate  decimal(18,2)        NOT NULL CONSTRAINT DF_AgrRates_ExtraAdult DEFAULT(0),
    ExtraChildRate  decimal(18,2)        NOT NULL CONSTRAINT DF_AgrRates_ExtraChild DEFAULT(0),
    IncludesBreakfast bit                NOT NULL CONSTRAINT DF_AgrRates_Breakfast DEFAULT(0),
    TaxApplicable   bit                  NOT NULL CONSTRAINT DF_AgrRates_Tax DEFAULT(1),
    CONSTRAINT PK_B2BAgreementRates PRIMARY KEY CLUSTERED (AgreementRateId),
    CONSTRAINT FK_AgrRates_Agreements FOREIGN KEY (AgreementId) REFERENCES dbo.B2BAgreements(AgreementId) ON DELETE CASCADE,
    CONSTRAINT FK_AgrRates_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES dbo.RoomTypes(RoomTypeId),
    CONSTRAINT FK_AgrRates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_AgrRates_Rate CHECK (ContractedRate >= 0)
);
CREATE UNIQUE INDEX UX_AgrRates_Agr_RoomType ON dbo.B2BAgreementRates(AgreementId, RoomTypeId);
GO

/* ================================ BOOKINGS ================================ */

CREATE TABLE dbo.Bookings
(
    BookingId          bigint IDENTITY(1,1) NOT NULL,
    TenantId           bigint               NOT NULL,
    BookingNumber      nvarchar(30)         NOT NULL,
    PrimaryGuestId     bigint               NOT NULL,
    BookingType        tinyint              NOT NULL CONSTRAINT DF_Bookings_Type DEFAULT(0), -- 0 Individual,1 Corporate
    BookingSource      tinyint              NOT NULL CONSTRAINT DF_Bookings_Source DEFAULT(0), -- 0 WalkIn,1 Phone,2 Website,3 Corporate,4 TravelAgent,5 OTA,6 Other
    BookingStatus      tinyint              NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT(0), -- 0 Pending,1 Confirmed,2 CheckedIn,3 CheckedOut,4 Cancelled,5 NoShow
    CorporateCustomerId bigint              NULL,
    AgreementId        bigint               NULL,
    CheckInDate        datetime2(0)         NOT NULL,
    CheckOutDate       datetime2(0)         NOT NULL,
    ActualCheckIn      datetime2(0)         NULL,
    ActualCheckOut     datetime2(0)         NULL,
    NumberOfAdults     int                  NOT NULL CONSTRAINT DF_Bookings_Adults DEFAULT(1),
    NumberOfChildren   int                  NOT NULL CONSTRAINT DF_Bookings_Children DEFAULT(0),
    DiscountAmount     decimal(18,2)        NOT NULL CONSTRAINT DF_Bookings_Discount DEFAULT(0),
    TaxAmount          decimal(18,2)        NOT NULL CONSTRAINT DF_Bookings_Tax DEFAULT(0),
    DepositAmount      decimal(18,2)        NOT NULL CONSTRAINT DF_Bookings_Deposit DEFAULT(0),
    TotalAmount        decimal(18,2)        NOT NULL CONSTRAINT DF_Bookings_Total DEFAULT(0), -- estimated total at creation; final folio at checkout
    SpecialRequests    nvarchar(500)        NULL,
    Notes              nvarchar(500)        NULL,
    CheckedInBy        bigint               NULL,
    CheckedOutBy       bigint               NULL,
    CancelledAt        datetime2(0)         NULL,
    CancellationReason nvarchar(300)        NULL,
    CreatedAt          datetime2(0)         NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy          bigint               NULL,
    UpdatedAt          datetime2(0)         NULL,
    UpdatedBy          bigint               NULL,
    IsActive           bit                  NOT NULL CONSTRAINT DF_Bookings_IsActive DEFAULT(1),
    CONSTRAINT PK_Bookings PRIMARY KEY CLUSTERED (BookingId),
    CONSTRAINT FK_Bookings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_Bookings_Guests FOREIGN KEY (PrimaryGuestId) REFERENCES dbo.Guests(GuestId),
    CONSTRAINT FK_Bookings_CorpCust FOREIGN KEY (CorporateCustomerId) REFERENCES dbo.CorporateCustomers(CorporateCustomerId),
    CONSTRAINT FK_Bookings_Agreements FOREIGN KEY (AgreementId) REFERENCES dbo.B2BAgreements(AgreementId),
    CONSTRAINT CK_Bookings_Status CHECK (BookingStatus IN (0,1,2,3,4,5)),
    CONSTRAINT CK_Bookings_Source CHECK (BookingSource IN (0,1,2,3,4,5,6)),
    CONSTRAINT CK_Bookings_Type CHECK (BookingType IN (0,1)),
    CONSTRAINT CK_Bookings_Dates CHECK (CheckOutDate > CheckInDate),
    CONSTRAINT CK_Bookings_Adults CHECK (NumberOfAdults >= 1),
    CONSTRAINT CK_Bookings_Children CHECK (NumberOfChildren >= 0)
);
CREATE UNIQUE INDEX UX_Bookings_Tenant_Number ON dbo.Bookings(TenantId, BookingNumber);
CREATE INDEX IX_Bookings_Tenant_Status ON dbo.Bookings(TenantId, BookingStatus) INCLUDE(CheckInDate, CheckOutDate, PrimaryGuestId);
CREATE INDEX IX_Bookings_Tenant_CheckIn ON dbo.Bookings(TenantId, CheckInDate);
CREATE INDEX IX_Bookings_Tenant_CheckOut ON dbo.Bookings(TenantId, CheckOutDate);
CREATE INDEX IX_Bookings_CorpCust ON dbo.Bookings(CorporateCustomerId) WHERE CorporateCustomerId IS NOT NULL;
GO

-- PRD section 42: a booking may contain multiple rooms.
CREATE TABLE dbo.BookingRooms
(
    BookingRoomId    bigint IDENTITY(1,1) NOT NULL,
    BookingId        bigint               NOT NULL,
    TenantId         bigint               NOT NULL,
    RoomId           bigint               NOT NULL,
    RatePerNight     decimal(18,2)        NOT NULL,
    Nights           int                  NOT NULL,
    TotalRoomCharge  decimal(18,2)        NOT NULL,
    CONSTRAINT PK_BookingRooms PRIMARY KEY CLUSTERED (BookingRoomId),
    CONSTRAINT FK_BookingRooms_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId) ON DELETE CASCADE,
    CONSTRAINT FK_BookingRooms_Rooms FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(RoomId),
    CONSTRAINT CK_BookingRooms_Nights CHECK (Nights > 0),
    CONSTRAINT CK_BookingRooms_Rates CHECK (RatePerNight >= 0 AND TotalRoomCharge >= 0)
);
CREATE INDEX IX_BookingRooms_Room_Booking ON dbo.BookingRooms(RoomId, BookingId); -- availability conflict scans
CREATE INDEX IX_BookingRooms_Tenant ON dbo.BookingRooms(TenantId);
GO

CREATE TABLE dbo.BookingGuests
(
    BookingGuestId bigint IDENTITY(1,1) NOT NULL,
    BookingId      bigint               NOT NULL,
    TenantId       bigint               NOT NULL,
    GuestId        bigint               NOT NULL,
    CONSTRAINT PK_BookingGuests PRIMARY KEY CLUSTERED (BookingGuestId),
    CONSTRAINT FK_BookingGuests_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId) ON DELETE CASCADE,
    CONSTRAINT FK_BookingGuests_Guests FOREIGN KEY (GuestId) REFERENCES dbo.Guests(GuestId)
);
CREATE UNIQUE INDEX UX_BookingGuests ON dbo.BookingGuests(BookingId, GuestId);
GO

CREATE TABLE dbo.AccompanyGuests
(
    AccompanyGuestId bigint IDENTITY(1,1) NOT NULL,
    BookingId        bigint               NOT NULL,
    TenantId         bigint               NOT NULL,
    FullName         nvarchar(150)        NOT NULL,
    IdType           tinyint              NULL,
    IdNumber         nvarchar(60)         NULL,
    Relationship     nvarchar(60)         NULL,
    Phone            nvarchar(30)         NULL,
    Age              int                  NULL,
    CreatedAt        datetime2(0)         NOT NULL CONSTRAINT DF_AccGst_CreatedAt DEFAULT(sysutcdatetime()),
    CONSTRAINT PK_AccompanyGuests PRIMARY KEY CLUSTERED (AccompanyGuestId),
    CONSTRAINT FK_AccGst_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId) ON DELETE CASCADE,
    CONSTRAINT CK_AccGst_Age CHECK (Age IS NULL OR (Age >= 0 AND Age <= 120))
);
CREATE INDEX IX_AccGst_Booking ON dbo.AccompanyGuests(BookingId);
GO

/* ============================ CHARGES / FOLIO =============================
   Design note (PRD sections 22-23 + 41): FoodBills are modelled as Charges rows
   with ChargeType = 1 (Food). The view below exposes them as "food bills" so the
   PRD entity remains queryable without duplicate storage.
   ========================================================================== */

CREATE TABLE dbo.Charges
(
    ChargeId       bigint IDENTITY(1,1) NOT NULL,
    TenantId       bigint               NOT NULL,
    BookingId      bigint               NOT NULL,
    ChargeType     tinyint              NOT NULL, -- 0 Room,1 Food,2 Laundry,3 ExtraBed,4 AdditionalGuest,5 Transport,6 MiniBar,7 Other,8 Adjustment
    Description    nvarchar(200)        NOT NULL,
    ChargeDate     datetime2(0)         NOT NULL CONSTRAINT DF_Charges_Date DEFAULT(sysutcdatetime()),
    Quantity       decimal(9,2)         NOT NULL CONSTRAINT DF_Charges_Qty DEFAULT(1),
    UnitPrice      decimal(18,2)        NOT NULL,
    DiscountAmount decimal(18,2)        NOT NULL CONSTRAINT DF_Charges_Discount DEFAULT(0),
    TaxAmount      decimal(18,2)        NOT NULL CONSTRAINT DF_Charges_Tax DEFAULT(0),
    TotalAmount    decimal(18,2)        NOT NULL, -- maintained by application: qty*unit-discount+tax
    CreatedAt      datetime2(0)         NOT NULL CONSTRAINT DF_Charges_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy      bigint               NULL,
    CONSTRAINT PK_Charges PRIMARY KEY CLUSTERED (ChargeId),
    CONSTRAINT FK_Charges_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
    CONSTRAINT FK_Charges_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_Charges_Type CHECK (ChargeType IN (0,1,2,3,4,5,6,7,8)),
    CONSTRAINT CK_Charges_Positive CHECK (Quantity > 0 AND UnitPrice >= 0 AND DiscountAmount >= 0 AND TaxAmount >= 0 AND TotalAmount >= 0)
);
CREATE INDEX IX_Charges_Booking ON dbo.Charges(BookingId);
CREATE INDEX IX_Charges_Tenant_Type ON dbo.Charges(TenantId, ChargeType, ChargeDate);
GO

CREATE VIEW dbo.vw_FoodBills
AS
SELECT  c.ChargeId    AS FoodBillId,
        c.TenantId,
        c.BookingId,
        b.BookingNumber,
        c.Description AS ItemName,
        c.Quantity,
        c.UnitPrice,
        c.TotalAmount,
        c.ChargeDate  AS ServedDate,
        c.CreatedAt
FROM dbo.Charges c
JOIN dbo.Bookings b ON b.BookingId = c.BookingId
WHERE c.ChargeType = 1;
GO

/* ============================ BILLING & PAYMENTS ========================== */

CREATE TABLE dbo.Invoices
(
    InvoiceId          bigint IDENTITY(1,1) NOT NULL,
    TenantId           bigint               NOT NULL,
    InvoiceNumber      nvarchar(30)         NOT NULL,
    BookingId          bigint               NULL,
    GuestId            bigint               NULL,
    CorporateCustomerId bigint              NULL, -- set for corporate invoices billed to the company
    InvoiceType        tinyint              NOT NULL CONSTRAINT DF_Invoices_Type DEFAULT(0), -- 0 Guest,1 Corporate
    InvoiceDate        datetime2(0)         NOT NULL CONSTRAINT DF_Invoices_Date DEFAULT(sysutcdatetime()),
    DueDate            date                 NULL,
    Subtotal           decimal(18,2)        NOT NULL,
    DiscountTotal      decimal(18,2)        NOT NULL CONSTRAINT DF_Invoices_Discount DEFAULT(0),
    TaxTotal           decimal(18,2)        NOT NULL CONSTRAINT DF_Invoices_Tax DEFAULT(0),
    TotalAmount        decimal(18,2)        NOT NULL,
    PaidAmount         decimal(18,2)        NOT NULL CONSTRAINT DF_Invoices_Paid DEFAULT(0),
    BalanceDue         decimal(18,2)        NOT NULL,
    Status             tinyint              NOT NULL CONSTRAINT DF_Invoices_Status DEFAULT(0), -- 0 Draft,1 Issued,2 PartiallyPaid,3 Paid,4 Cancelled
    Notes              nvarchar(500)        NULL,
    IssuedBy           bigint               NULL,
    IssuedAt           datetime2(0)         NULL,
    CancelledAt        datetime2(0)         NULL,
    CreatedAt          datetime2(0)         NOT NULL CONSTRAINT DF_Invoices_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy          bigint               NULL,
    UpdatedAt          datetime2(0)         NULL,
    UpdatedBy          bigint               NULL,
    IsActive           bit                  NOT NULL CONSTRAINT DF_Invoices_IsActive DEFAULT(1),
    CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (InvoiceId),
    CONSTRAINT FK_Invoices_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_Invoices_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
    CONSTRAINT FK_Invoices_Guests FOREIGN KEY (GuestId) REFERENCES dbo.Guests(GuestId),
    CONSTRAINT FK_Invoices_CorpCust FOREIGN KEY (CorporateCustomerId) REFERENCES dbo.CorporateCustomers(CorporateCustomerId),
    CONSTRAINT CK_Invoices_Status CHECK (Status IN (0,1,2,3,4)),
    CONSTRAINT CK_Invoices_Type CHECK (InvoiceType IN (0,1)),
    CONSTRAINT CK_Invoices_Money CHECK (Subtotal >= 0 AND DiscountTotal >= 0 AND TaxTotal >= 0 AND TotalAmount >= 0 AND PaidAmount >= 0)
    -- NOTE: no CHECK on BalanceDue >= 0 because explicit overpayments must be allowed (PRD section 25).
);
CREATE UNIQUE INDEX UX_Invoices_Tenant_Number ON dbo.Invoices(TenantId, InvoiceNumber);
CREATE INDEX IX_Invoices_Tenant_Status ON dbo.Invoices(TenantId, Status) INCLUDE(TotalAmount, PaidAmount, BalanceDue);
CREATE INDEX IX_Invoices_Booking ON dbo.Invoices(BookingId) WHERE BookingId IS NOT NULL;
CREATE INDEX IX_Invoices_CorpCust ON dbo.Invoices(CorporateCustomerId, Status) WHERE CorporateCustomerId IS NOT NULL;
GO

CREATE TABLE dbo.InvoiceItems
(
    InvoiceItemId  bigint IDENTITY(1,1) NOT NULL,
    InvoiceId      bigint               NOT NULL,
    TenantId       bigint               NOT NULL,
    Description    nvarchar(200)        NOT NULL,
    ChargeDate     datetime2(0)         NULL,
    Quantity       decimal(9,2)         NOT NULL CONSTRAINT DF_InvItem_Qty DEFAULT(1),
    UnitPrice      decimal(18,2)        NOT NULL,
    DiscountAmount decimal(18,2)        NOT NULL CONSTRAINT DF_InvItem_Discount DEFAULT(0),
    TaxAmount      decimal(18,2)        NOT NULL CONSTRAINT DF_InvItem_Tax DEFAULT(0),
    TotalAmount    decimal(18,2)        NOT NULL,
    SourceChargeId bigint               NULL, -- traceability back to folio charge
    CONSTRAINT PK_InvoiceItems PRIMARY KEY CLUSTERED (InvoiceItemId),
    CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices(InvoiceId) ON DELETE CASCADE,
    CONSTRAINT FK_InvoiceItems_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_InvoiceItems_Charges FOREIGN KEY (SourceChargeId) REFERENCES dbo.Charges(ChargeId),
    CONSTRAINT CK_InvItem_Positive CHECK (Quantity > 0 AND UnitPrice >= 0 AND TotalAmount >= 0)
);
CREATE INDEX IX_InvoiceItems_Invoice ON dbo.InvoiceItems(InvoiceId);
GO

CREATE TABLE dbo.Payments
(
    PaymentId            bigint IDENTITY(1,1) NOT NULL,
    TenantId             bigint               NOT NULL,
    InvoiceId            bigint               NOT NULL,
    Amount               decimal(18,2)        NOT NULL,
    PaymentMethod        tinyint              NOT NULL, -- 0 Cash,1 BankTransfer,2 Card,3 Online,4 Other
    TransactionReference nvarchar(100)        NULL,
    PaymentDate          datetime2(0)         NOT NULL CONSTRAINT DF_Payments_Date DEFAULT(sysutcdatetime()),
    Notes                nvarchar(300)        NULL,
    ReceivedBy           bigint               NULL,
    CreatedAt            datetime2(0)         NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy            bigint               NULL,
    IsReversed           bit                  NOT NULL CONSTRAINT DF_Payments_Reversed DEFAULT(0),
    ReversedAt           datetime2(0)         NULL,
    CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED (PaymentId),
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices(InvoiceId),
    CONSTRAINT FK_Payments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_Payments_Method CHECK (PaymentMethod IN (0,1,2,3,4)),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0)
);
CREATE INDEX IX_Payments_Invoice ON dbo.Payments(InvoiceId);
CREATE INDEX IX_Payments_Tenant_Date ON dbo.Payments(TenantId, PaymentDate) INCLUDE(PaymentMethod, Amount);
GO

/* ================================= PAYROLL ================================ */

CREATE TABLE dbo.PayrollRuns
(
    PayrollRunId    bigint IDENTITY(1,1) NOT NULL,
    TenantId        bigint               NOT NULL,
    PayrollMonth    date                 NOT NULL, -- always day 01 of the month
    PeriodStart     date                 NOT NULL,
    PeriodEnd       date                 NOT NULL,
    Status          tinyint              NOT NULL CONSTRAINT DF_PayrollRuns_Status DEFAULT(0), -- 0 Draft,1 Finalized,2 Paid
    TotalGross      decimal(18,2)        NOT NULL CONSTRAINT DF_PR_TotalGross DEFAULT(0),
    TotalDeductions decimal(18,2)        NOT NULL CONSTRAINT DF_PR_TotalDed DEFAULT(0),
    TotalNet        decimal(18,2)        NOT NULL CONSTRAINT DF_PR_TotalNet DEFAULT(0),
    FinalizedAt     datetime2(0)         NULL,
    FinalizedBy     bigint               NULL,
    PaidAt          datetime2(0)         NULL,
    Notes           nvarchar(500)        NULL,
    CreatedAt       datetime2(0)         NOT NULL CONSTRAINT DF_PR_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy       bigint               NULL,
    UpdatedAt       datetime2(0)         NULL,
    UpdatedBy       bigint               NULL,
    IsActive        bit                  NOT NULL CONSTRAINT DF_PR_IsActive DEFAULT(1),
    CONSTRAINT PK_PayrollRuns PRIMARY KEY CLUSTERED (PayrollRunId),
    CONSTRAINT FK_PayrollRuns_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT CK_PR_Status CHECK (Status IN (0,1,2))
);
CREATE UNIQUE INDEX UX_PayrollRuns_Tenant_Month ON dbo.PayrollRuns(TenantId, PayrollMonth) WHERE IsActive = 1;
GO

CREATE TABLE dbo.PayrollItems
(
    PayrollItemId     bigint IDENTITY(1,1) NOT NULL,
    PayrollRunId      bigint               NOT NULL,
    TenantId          bigint               NOT NULL,
    EmployeeId        bigint               NOT NULL,
    BasicSalary       decimal(18,2)        NOT NULL,
    Allowances        decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Allowances DEFAULT(0),
    OvertimeAmount    decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Overtime DEFAULT(0),
    BonusAmount       decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Bonus DEFAULT(0),
    Deductions        decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Deductions DEFAULT(0),
    AdvanceDeduction  decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Advance DEFAULT(0),
    LoanDeduction     decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Loan DEFAULT(0),
    AbsenceDeduction  decimal(18,2)        NOT NULL CONSTRAINT DF_PI_Absence DEFAULT(0),
    GrossSalary       decimal(18,2)        NOT NULL, -- app-maintained: basic+allow+ot+bonus
    NetSalary         decimal(18,2)        NOT NULL, -- app-maintained: gross-deductions-advance-loan-absence
    Remarks           nvarchar(300)        NULL,
    CONSTRAINT PK_PayrollItems PRIMARY KEY CLUSTERED (PayrollItemId),
    CONSTRAINT FK_PI_PayrollRuns FOREIGN KEY (PayrollRunId) REFERENCES dbo.PayrollRuns(PayrollRunId),
    CONSTRAINT FK_PI_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_PI_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT CK_PI_Nonnegative CHECK (BasicSalary >= 0 AND Allowances >= 0 AND OvertimeAmount >= 0 AND BonusAmount >= 0
                                        AND Deductions >= 0 AND AdvanceDeduction >= 0 AND LoanDeduction >= 0
                                        AND AbsenceDeduction >= 0 AND GrossSalary >= 0 AND NetSalary >= 0)
);
CREATE UNIQUE INDEX UX_PI_Run_Employee ON dbo.PayrollItems(PayrollRunId, EmployeeId);
GO

/* =============================== HOUSEKEEPING ============================= */

CREATE TABLE dbo.HousekeepingTasks
(
    TaskId              bigint IDENTITY(1,1) NOT NULL,
    TenantId            bigint               NOT NULL,
    RoomId              bigint               NOT NULL,
    BookingId           bigint               NULL,
    TaskType            tinyint              NOT NULL CONSTRAINT DF_HkT_TaskType DEFAULT(0), -- 0 Cleaning,1 Inspection,2 Maintenance
    Status              tinyint              NOT NULL CONSTRAINT DF_HkT_Status DEFAULT(0), -- 0 Pending,1 InProgress,2 Completed,3 Cancelled
    AssignedToEmployeeId bigint              NULL,
    ScheduledFor        datetime2(0)         NOT NULL CONSTRAINT DF_HkT_SchedFor DEFAULT(sysutcdatetime()),
    StartedAt           datetime2(0)         NULL,
    CompletedAt         datetime2(0)         NULL,
    CompletedByUserId   bigint               NULL,
    Notes               nvarchar(500)        NULL,
    CreatedAt           datetime2(0)         NOT NULL CONSTRAINT DF_HkT_CreatedAt DEFAULT(sysutcdatetime()),
    CreatedBy           bigint               NULL,
    IsActive            bit                  NOT NULL CONSTRAINT DF_HkT_IsActive DEFAULT(1),
    CONSTRAINT PK_HousekeepingTasks PRIMARY KEY CLUSTERED (TaskId),
    CONSTRAINT FK_HkT_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_HkT_Rooms FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(RoomId),
    CONSTRAINT FK_HkT_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
    CONSTRAINT FK_HkT_Employees FOREIGN KEY (AssignedToEmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT FK_HkT_Users FOREIGN KEY (CompletedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_HkT_TaskType CHECK (TaskType IN (0,1,2)),
    CONSTRAINT CK_HkT_Status CHECK (Status IN (0,1,2,3))
);
CREATE INDEX IX_HkT_Tenant_Status ON dbo.HousekeepingTasks(TenantId, Status) INCLUDE(RoomId, TaskType);
CREATE INDEX IX_HkT_Room ON dbo.HousekeepingTasks(RoomId);
GO

/* ============================== NOTIFICATIONS ============================= */

CREATE TABLE dbo.Notifications
(
    NotificationId bigint IDENTITY(1,1) NOT NULL,
    TenantId       bigint               NULL,
    UserId         bigint               NULL, -- NULL => broadcast to tenant admins
    Title          nvarchar(200)        NOT NULL,
    Body           nvarchar(max)        NULL,
    Type           tinyint              NOT NULL CONSTRAINT DF_Notify_Type DEFAULT(0), -- see enum seed notes
    Channel        tinyint              NOT NULL CONSTRAINT DF_Notify_Channel DEFAULT(0), -- 0 InApp,1 Email,2 SMS,3 WhatsApp
    Status         tinyint              NOT NULL CONSTRAINT DF_Notify_Status DEFAULT(0), -- 0 Pending,1 Sent,2 Read,3 Failed
    EntityType     nvarchar(60)         NULL,
    EntityId       bigint               NULL,
    CreatedAt      datetime2(0)         NOT NULL CONSTRAINT DF_Notify_CreatedAt DEFAULT(sysutcdatetime()),
    SentAt         datetime2(0)         NULL,
    ReadAt         datetime2(0)         NULL,
    CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (NotificationId),
    CONSTRAINT FK_Notify_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
    CONSTRAINT FK_Notify_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
CREATE INDEX IX_Notify_User_Status ON dbo.Notifications(UserId, Status) WHERE UserId IS NOT NULL;
CREATE INDEX IX_Notify_Tenant ON dbo.Notifications(TenantId);
GO

/* ================================ AUDIT LOGS ==============================
   Append-only. No UPDATE/DELETE path exists in the application; enforced by
   convention and by revoking direct table writes from app-facing roles in
   hardened deployments. Ordinary users can never reach these APIs.
   ========================================================================== */

CREATE TABLE dbo.AuditLogs
(
    AuditLogId    bigint IDENTITY(1,1) NOT NULL,
    TenantId      bigint               NULL,
    UserId        bigint               NULL,
    UserName      nvarchar(150)        NULL,
    Action        nvarchar(60)         NOT NULL, -- Create/Update/Delete/CheckIn/CheckOut/Finalize/etc.
    EntityName    nvarchar(60)         NOT NULL,
    EntityId      nvarchar(50)         NULL,
    OldValues     nvarchar(max)        NULL,
    NewValues     nvarchar(max)        NULL,
    IpAddress     nvarchar(50)         NULL,
    CorrelationId nvarchar(50)         NULL,
    CreatedAt     datetime2(0)         NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT(sysutcdatetime()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (AuditLogId)
);
CREATE INDEX IX_AuditLogs_Tenant_Time ON dbo.AuditLogs(TenantId, CreatedAt DESC);
CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs(EntityName, EntityId);
GO

PRINT 'HotelSaaS_DB schema created successfully.';
GO
