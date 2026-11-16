/* ============================================================================
   HotelSaaS_DB - Reference + Demo Seed Data
   ----------------------------------------------------------------------------
   * PasswordHash left NULL for seeded users: the API's first-run seeder assigns
     the hashed default password (Admin@123) using the production PBKDF2 code,
     guaranteeing hash format consistency. Change passwords after first login.
   * Dates are relative to run time so dashboards always show live activity.
   ========================================================================== */
USE HotelSaaS_DB;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRAN;

/* ------------------------------ ROLES ----------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
    INSERT INTO dbo.Roles (RoleId, Name) VALUES
        (1,'SuperAdmin'),(2,'TenantAdmin'),(3,'Manager'),(4,'FrontDesk'),
        (5,'Finance'),(6,'HR'),(7,'Housekeeping');

/* --------------------------- SYSTEM SETTINGS ---------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'PlatformName')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description)
    VALUES (N'PlatformName', N'HotelSaaS', N'Display name of the SaaS platform'),
           (N'SupportEmail', N'support@hotelsaas.local', N'Platform support contact');

/* ----------------------------- SUPER ADMIN ------------------------------ */
DECLARE @superAdmin bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'superadmin@hotelsaas.com')
BEGIN
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone)
    VALUES (NULL, N'Platform Administrator', N'superadmin@hotelsaas.com', N'+92-300-0000000');
    SET @superAdmin = SCOPE_IDENTITY();
    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@superAdmin, 1);
END
ELSE
    SELECT @superAdmin = UserId FROM dbo.Users WHERE Email = N'superadmin@hotelsaas.com';

/* ============================ TENANT A : SAIBAAN ========================= */
DECLARE @tA bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantCode = 'SAIBAAN')
    INSERT INTO dbo.Tenants (TenantCode, BusinessName, LegalName, Status, ContactEmail, ContactPhone,
                             Address, City, Country, TimeZone, Currency, CreatedBy)
    VALUES ('SAIBAAN', N'Saibaan Guest House', N'Saibaan Hospitality (Pvt) Ltd', 1,
            N'info@saibaan.example', N'+92-42-111222333',
            N'12-C Main Boulevard, Gulberg III', N'Lahore', N'Pakistan',
            N'Pakistan Standard Time', 'PKR', @superAdmin);
SELECT @tA = TenantId FROM dbo.Tenants WHERE TenantCode = 'SAIBAAN';

/* ---- Tenant A users ---- */
DECLARE @fdA bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'admin@saibaan.com')
BEGIN
    DECLARE @uid table (UserId bigint);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy)
    OUTPUT inserted.UserId INTO @uid
    VALUES (@tA, N'Ahmed Owner', N'admin@saibaan.com', N'+92-321-1111111', @superAdmin);
    INSERT INTO dbo.UserRoles SELECT UserId, 2 FROM @uid;

    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tA, N'Ayesha Manager', N'manager@saibaan.com', N'+92-321-2222222', @superAdmin);
    INSERT INTO dbo.UserRoles VALUES (SCOPE_IDENTITY(), 3);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tA, N'Bilal Front Desk', N'frontdesk@saibaan.com', N'+92-321-3333333', @superAdmin);
    SET @fdA = SCOPE_IDENTITY();
    INSERT INTO dbo.UserRoles VALUES (@fdA, 4);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tA, N'Sadia Finance', N'finance@saibaan.com', N'+92-321-4444444', @superAdmin);
    INSERT INTO dbo.UserRoles VALUES (SCOPE_IDENTITY(), 5);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tA, N'Naveed HR', N'hr@saibaan.com', N'+92-321-5555555', @superAdmin);
    INSERT INTO dbo.UserRoles VALUES (SCOPE_IDENTITY(), 6);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tA, N'Rashida HK', N'housekeeping@saibaan.com', N'+92-321-6666666', @superAdmin);
    INSERT INTO dbo.UserRoles VALUES (SCOPE_IDENTITY(), 7);
END
ELSE
    SELECT @fdA = UserId FROM dbo.Users WHERE Email = N'frontdesk@saibaan.com';

/* ---- Tenant A company info + property settings ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.CompanyInformations WHERE TenantId = @tA)
    INSERT INTO dbo.CompanyInformations (TenantId, BusinessName, DisplayName, LegalName, RegistrationNumber,
        TaxNumber, Address, City, Country, PostalCode, Phone, Email, Website,
        InvoiceHeader, InvoiceFooter, TaxPercentage, TaxEnabled, CreatedBy)
    VALUES (@tA, N'Saibaan Guest House', N'Saibaan Guest House', N'Saibaan Hospitality (Pvt) Ltd',
        N'REG-LHR-2019-448821', N'TAX-3345678',
        N'12-C Main Boulevard, Gulberg III', N'Lahore', N'Pakistan', N'54660',
        N'+92-42-111222333', N'info@saibaan.example', N'https://saibaan.example',
        N'Saibaan Guest House - Guest Folio', N'Thank you for staying with us. Please retain this invoice.', 5.00, 1, @superAdmin);

IF NOT EXISTS (SELECT 1 FROM dbo.PropertySettings WHERE TenantId = @tA)
    INSERT INTO dbo.PropertySettings (TenantId, PropertyName, PropertyType, NumberOfFloors, CurrencyCode, TimeZoneId,
        TaxRatePercent, DefaultBreakfastIncluded, BookingPolicyText, CancellationPolicyText, PaymentPolicyText, CreatedBy)
    VALUES (@tA, N'Saibaan Guest House', N'GuestHouse', 3, 'PKR', N'Pakistan Standard Time',
        5.00, 1,
        N'Check-in 2:00 PM. Valid ID required at check-in.',
        N'Free cancellation up to 24 hours before check-in.',
        N'Cash and card accepted. Corporates billed per agreement terms.', @superAdmin);

/* ---- Departments ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE TenantId = @tA AND Name = N'Front Desk')
    INSERT INTO dbo.Departments (TenantId, Name) VALUES
        (@tA,N'Front Desk'),(@tA,N'Housekeeping'),(@tA,N'Accounts'),(@tA,N'Kitchen'),
        (@tA,N'Management'),(@tA,N'Maintenance'),(@tA,N'Security');

/* ---- Room types ---- */
DECLARE @rtStandard bigint, @rtDeluxe bigint, @rtSuite bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.RoomTypes WHERE TenantId = @tA AND Name = N'Standard')
BEGIN
    INSERT INTO dbo.RoomTypes (TenantId, Name, Description, MaxOccupancy, BasePrice, ExtraAdultPrice, ExtraChildPrice,
                               NumberOfBeds, BedConfiguration, Amenities, SortOrder, CreatedBy)
    VALUES (@tA, N'Standard', N'Comfortable standard room with AC and TV', 2, 5000, 1500, 750, 1, N'1 Queen',
            N'AC,TV,WiFi,Hot Water,Breakfast Included', 1, @superAdmin);
    SET @rtStandard = SCOPE_IDENTITY();
    INSERT INTO dbo.RoomTypes (TenantId, Name, Description, MaxOccupancy, BasePrice, ExtraAdultPrice, ExtraChildPrice,
                               NumberOfBeds, BedConfiguration, Amenities, SortOrder, CreatedBy)
    VALUES (@tA, N'Deluxe', N'Spacious deluxe room with work desk and mini fridge', 3, 7000, 2000, 1000, 1, N'1 King',
            N'AC,TV,WiFi,MiniFridge,Sofa,Breakfast Included', 2, @superAdmin);
    SET @rtDeluxe = SCOPE_IDENTITY();
    INSERT INTO dbo.RoomTypes (TenantId, Name, Description, MaxOccupancy, BasePrice, ExtraAdultPrice, ExtraChildPrice,
                               NumberOfBeds, BedConfiguration, Amenities, SortOrder, CreatedBy)
    VALUES (@tA, N'Family Suite', N'Two-bedroom suite ideal for families', 6, 12000, 2500, 1250, 3, N'1 King + 2 Single',
            N'AC,TV,WiFi,Lounge,Kitchenette,Dining', 3, @superAdmin);
    SET @rtSuite = SCOPE_IDENTITY();

    /* ---- Rooms: 5x Standard (101-105), 3x Deluxe (201-203), 1x Suite (301) ---- */
    INSERT INTO dbo.Rooms (TenantId, RoomTypeId, RoomNumber, Floor, CreatedBy) VALUES
        (@tA,@rtStandard,'101',1,@superAdmin),(@tA,@rtStandard,'102',1,@superAdmin),
        (@tA,@rtStandard,'103',1,@superAdmin),(@tA,@rtStandard,'104',1,@superAdmin),
        (@tA,@rtStandard,'105',1,@superAdmin),
        (@tA,@rtDeluxe,'201',2,@superAdmin),(@tA,@rtDeluxe,'202',2,@superAdmin),
        (@tA,@rtDeluxe,'203',2,@superAdmin),
        (@tA,@rtSuite,'301',3,@superAdmin);
END

/* ---- Guests ---- */
DECLARE @gAhmed bigint, @gSara bigint, @gBilal bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.Guests WHERE TenantId = @tA AND Phone = N'+92-300-1234567')
BEGIN
    INSERT INTO dbo.Guests (TenantId, FirstName, LastName, Gender, Nationality, IdType, IdNumber, Phone, Email, City, Country, CreatedBy)
    VALUES (@tA, N'Ahmed', N'Raza', 0, N'Pakistani', 0, N'35201-1234567-1', N'+92-300-1234567', N'ahmed.raza@example.com', N'Lahore', N'Pakistan', @superAdmin);
    SET @gAhmed = SCOPE_IDENTITY();
    INSERT INTO dbo.Guests (TenantId, FirstName, LastName, Gender, Nationality, IdType, IdNumber, Phone, Email, City, Country, CreatedBy)
    VALUES (@tA, N'Sara', N'Khan', 1, N'Pakistani', 1, N'AB1234567', N'+92-301-7654321', N'sara.khan@example.com', N'Islamabad', N'Pakistan', @superAdmin);
    SET @gSara = SCOPE_IDENTITY();
    INSERT INTO dbo.Guests (TenantId, FirstName, LastName, Gender, Nationality, IdType, IdNumber, Phone, City, Country, CreatedBy)
    VALUES (@tA, N'Bilal', N'Hussain', 0, N'Pakistani', 0, N'37405-9876543-9', N'+92-302-5559911', N'Multan', N'Pakistan', @superAdmin);
    SET @gBilal = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @gAhmed = GuestId FROM dbo.Guests WHERE TenantId=@tA AND Phone=N'+92-300-1234567';
    SELECT @gSara  = GuestId FROM dbo.Guests WHERE TenantId=@tA AND Phone=N'+92-301-7654321';
    SELECT @gBilal = GuestId FROM dbo.Guests WHERE TenantId=@tA AND Phone=N'+92-302-5559911';
END

/* ---- Corporate customer + agreement ---- */
DECLARE @corpABC bigint, @agrABC bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.CorporateCustomers WHERE TenantId = @tA AND CompanyName = N'ABC Corporation')
BEGIN
    INSERT INTO dbo.CorporateCustomers (TenantId, CompanyName, RegistrationNumber, TaxNumber, ContactPerson,
        Phone, Email, Address, BillingAddress, Status, Notes, CreatedBy)
    VALUES (@tA, N'ABC Corporation', N'REG-KHI-777001', N'TAX-9988771', N'Mr. Imran Sheikh',
        N'+92-21-34567890', N'accounts@abccorp.example', N'45 I.I. Chundrigar Road, Karachi',
        N'45 I.I. Chundrigar Road, Karachi', 1, N'Regular corporate client since 2024.', @superAdmin);
    SET @corpABC = SCOPE_IDENTITY();

    INSERT INTO dbo.B2BAgreements (TenantId, CorporateCustomerId, AgreementNumber, StartDate, EndDate, Status,
        PaymentTermsDays, CreditLimit, IncludesBreakfast, SpecialFacilities, CancellationRules, Notes, CreatedBy)
    VALUES (@tA, @corpABC, N'AGR-SBN-001',
        CAST(FORMAT(DATEADD(MONTH,-6,GETUTCDATE()),'yyyy-MM-01') AS date),
        CAST(FORMAT(DATEADD(MONTH,6,GETUTCDATE()),'yyyy-MM-01') AS date),
        1, 30, 200000, 1,
        N'Free airport pickup for stays of 3+ nights', N'Free cancellation up to 48h before arrival',
        N'Annual corporate agreement - negotiated rates.', @superAdmin);
    SET @agrABC = SCOPE_IDENTITY();

    INSERT INTO dbo.B2BAgreementRates (AgreementId, TenantId, RoomTypeId, ContractedRate, IncludesBreakfast, TaxApplicable)
    SELECT @agrABC, @tA, RoomTypeId,
           CASE Name WHEN N'Standard' THEN 5000 WHEN N'Deluxe' THEN 7000 ELSE 11000 END, 1, 1
    FROM dbo.RoomTypes WHERE TenantId = @tA;
END
ELSE
BEGIN
    SELECT @corpABC = CorporateCustomerId FROM dbo.CorporateCustomers WHERE TenantId=@tA AND CompanyName=N'ABC Corporation';
    SELECT @agrABC  = AgreementId FROM dbo.B2BAgreements WHERE TenantId=@tA AND AgreementNumber=N'AGR-SBN-001';
END

/* ---- Employees ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @tA)
    INSERT INTO dbo.Employees (TenantId, EmployeeCode, FirstName, LastName, CNIC, Phone, DepartmentId, Designation,
        JoiningDate, EmploymentStatus, BasicSalary, BankName, BankAccountNumber, EmergencyContactName, EmergencyContactPhone, CreatedBy)
    SELECT @tA, x.Code, x.FirstName, x.LastName, x.CNIC, x.Phone, d.DepartmentId, x.Designation,
           x.JoiningDate, 1, x.Salary, N'Meezan Bank', x.Account, x.EmergencyName, x.EmergencyPhone, @superAdmin
    FROM (VALUES
        ('E-001','Zafar','Ali',   N'35201-5550101-5',N'+92-333-1010101',N'Front Desk',  N'Front Desk Executive','2024-03-01',45000,'PK36MEZN0001234567890101',N'Kiran Zafar',N'+92-333-1010199'),
        ('E-002','Naveed','Iqbal', N'35202-5550102-3',N'+92-333-2020202',N'Housekeeping',N'Housekeeper',        '2024-05-15',35000,'PK36MEZN0001234567890102',N'Fatima Naveed',N'+92-333-2020299'),
        ('E-003','Kamran','Shah',  N'35201-5550103-1',N'+92-333-3030303',N'Kitchen',     N'Head Cook',          '2023-11-01',50000,'PK36MEZN0001234567890103',N'Ayesha Kamran',N'+92-333-3030399'),
        ('E-004','Sadia','Noor',   N'35201-5550104-9',N'+92-333-4040404',N'Accounts',    N'Accountant',         '2024-01-10',65000,'PK36MEZN0001234567890104',N'Bilal Noor',N'+92-333-4040499'),
        ('E-005','Tariq','Mehmood',N'35201-5550105-7',N'+92-333-5050505',N'Management',  N'Operations Manager',  '2023-06-20',90000,'PK36MEZN0001234567890105',N'Hina Tariq',N'+92-333-5050599'),
        ('E-006','Rashid','Khan',  N'35201-5550106-5',N'+92-333-6060606',N'Maintenance', N'Technician',          '2024-08-01',40000,'PK36MEZN0001234567890106',N'Sana Rashid',N'+92-333-6060699')
    ) x(Code,FirstName,LastName,CNIC,Phone,Department,Designation,JoiningDate,Salary,Account,EmergencyName,EmergencyPhone)
    JOIN dbo.Departments d ON d.TenantId = @tA AND d.Name = x.Department;

/* ---- DEMO ACTIVITY ----------------------------------------------------- */
/* BK-1000001: completed stay, invoiced & fully paid                        */
/* BK-1000002: current in-house stay (folio open)                           */
/* BK-1000003: upcoming confirmed reservation                               */
/* BK-1000004: upcoming corporate reservation under ABC agreement           */
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE TenantId = @tA)
BEGIN
    DECLARE @now datetime2(0) = GETUTCDATE();
    DECLARE @r101 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='101'),
            @r102 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='102'),
            @r103 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='103'),
            @r104 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='104'),
            @r202 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='202'),
            @r301 bigint=(SELECT RoomId FROM dbo.Rooms WHERE TenantId=@tA AND RoomNumber='301');

    /* --- BK-1000001: Ahmed Raza, Standard 104, 2 nights ended 3 days ago --- */
    DECLARE @b1In datetime2(0)=DATEADD(HOUR,-34,DATEADD(DAY,-2,@now)), -- approx 2 days ago ~2pm
            @b1Out datetime2(0)=DATEADD(HOUR,-10,@now);                -- today noon-ish past
    SET @b1In = DATEADD(DAY,-5,@now); SET @b1Out = DATEADD(DAY,-3,@now);
    INSERT INTO dbo.Bookings (TenantId, BookingNumber, PrimaryGuestId, BookingType, BookingSource, BookingStatus,
        CheckInDate, CheckOutDate, ActualCheckIn, ActualCheckOut, NumberOfAdults, NumberOfChildren,
        DiscountAmount, TaxAmount, DepositAmount, TotalAmount, CheckedInBy, CheckedOutBy, CreatedBy)
    VALUES (@tA,'BK-1000001',@gAhmed,0,0,3,@b1In,@b1Out,DATEADD(HOUR,14,CAST(CAST(@b1In AS date) AS datetime2(0))),
        DATEADD(HOUR,-3,@now),        1,0,0,550.00,0,11550,@fdA,@fdA,@superAdmin);

    DECLARE @br1 bigint;
    INSERT INTO dbo.BookingRooms (BookingId,TenantId,RoomId,RatePerNight,Nights,TotalRoomCharge)
    VALUES (SCOPE_IDENTITY(),@tA,@r104,5000,2,10000);
    SELECT @br1 = SCOPE_IDENTITY();
    UPDATE dbo.BookingRooms SET BookingId=(SELECT BookingId FROM dbo.Bookings WHERE TenantId=@tA AND BookingNumber='BK-1000001') WHERE BookingRoomId=@br1;

    DECLARE @b1 bigint=(SELECT BookingId FROM dbo.Bookings WHERE TenantId=@tA AND BookingNumber='BK-1000001');
    UPDATE dbo.BookingRooms SET BookingId=@b1 WHERE BookingRoomId=@br1;

    INSERT INTO dbo.Charges (TenantId,BookingId,ChargeType,Description,ChargeDate,Quantity,UnitPrice,DiscountAmount,TaxAmount,TotalAmount,CreatedBy) VALUES
        (@tA,@b1,0,N'Room charge - Standard 104',@b1In, 2,5000,0,0,10000,@fdA),
        (@tA,@b1,1,N'Breakfast buffet',          @b1In, 2,350, 0,0,700,  @fdA),
        (@tA,@b1,2,N'Laundry service',           DATEADD(DAY,1,@b1In),1,300,0,0,300,@fdA);

    DECLARE @inv1 bigint;
    INSERT INTO dbo.Invoices (TenantId,InvoiceNumber,BookingId,GuestId,CorporateCustomerId,InvoiceType,InvoiceDate,
        Subtotal,DiscountTotal,TaxTotal,TotalAmount,PaidAmount,BalanceDue,Status,IssuedBy,IssuedAt,CreatedBy)
    VALUES (@tA,'INV-1000001',@b1,@gAhmed,NULL,0,DATEADD(HOUR,-3,@now),
        11000,0,550,11550,11550,0,3,@fdA,DATEADD(HOUR,-3,@now),@fdA);
    SELECT @inv1=SCOPE_IDENTITY();
    INSERT INTO dbo.InvoiceItems (InvoiceId,TenantId,Description,ChargeDate,Quantity,UnitPrice,DiscountAmount,TaxAmount,TotalAmount,SourceChargeId)
    SELECT @inv1,@tA,c.Description,c.ChargeDate,c.Quantity,c.UnitPrice,c.DiscountAmount,c.TaxAmount,c.TotalAmount,c.ChargeId
    FROM dbo.Charges c WHERE c.BookingId=@b1;
    INSERT INTO dbo.Payments (TenantId,InvoiceId,Amount,PaymentMethod,TransactionReference,PaymentDate,ReceivedBy,CreatedBy)
    VALUES (@tA,@inv1,11550,0,N'CASH-0001',DATEADD(HOUR,-2,@now),@fdA,@fdA);

    /* --- BK-1000002: Bilal Hussain, Deluxe 202, in-house (-1d .. +3d) --- */
    DECLARE @b2In datetime2(0)=DATEADD(DAY,-1,@now), @b2Out datetime2(0)=DATEADD(DAY,3,@now);
    INSERT INTO dbo.Bookings (TenantId, BookingNumber, PrimaryGuestId, BookingType, BookingSource, BookingStatus,
        CheckInDate, CheckOutDate, ActualCheckIn, NumberOfAdults, NumberOfChildren,
        DiscountAmount, TaxAmount, DepositAmount, TotalAmount, CheckedInBy, CreatedBy)
    VALUES (@tA,'BK-1000002',@gBilal,0,0,2,@b2In,@b2Out,DATEADD(HOUR,16,CAST(CAST(@b2In AS date) AS datetime2(0))),
        2,1,0,1525.00,0,32025,@fdA,@superAdmin);
    DECLARE @b2 bigint=(SELECT BookingId FROM dbo.Bookings WHERE TenantId=@tA AND BookingNumber='BK-1000002');
    INSERT INTO dbo.BookingRooms (BookingId,TenantId,RoomId,RatePerNight,Nights,TotalRoomCharge) VALUES (@b2,@tA,@r202,7000,4,28000);
    INSERT INTO dbo.Charges (TenantId,BookingId,ChargeType,Description,ChargeDate,Quantity,UnitPrice,DiscountAmount,TaxAmount,TotalAmount,CreatedBy) VALUES
        (@tA,@b2,0,N'Room charge - Deluxe 202',@b2In,4,7000,0,0,28000,@fdA),
        (@tA,@b2,1,N'Dinner - BBQ platter',DATEADD(HOUR,20,CAST(CAST(@b2In AS date) AS datetime2(0))),1,2500,0,0,2500,@fdA);
    UPDATE dbo.Rooms SET Status=2, HousekeepingStatus=1 WHERE RoomId=@r202;

    /* --- BK-1000003: Sara Khan, Family Suite 301 (+9d .. +11d) --- */
    DECLARE @b3In datetime2(0)=DATEADD(DAY,9,@now), @b3Out datetime2(0)=DATEADD(DAY,11,@now);
    INSERT INTO dbo.Bookings (TenantId, BookingNumber, PrimaryGuestId, BookingType, BookingSource, BookingStatus,
        CheckInDate, CheckOutDate, NumberOfAdults, NumberOfChildren, DiscountAmount, TaxAmount, TotalAmount,
        SpecialRequests, CreatedBy)
    VALUES (@tA,'BK-1000003',@gSara,0,2,1,@b3In,@b3Out,2,2,0,1200.00,25200,N'Extra pillows and baby cot please',@superAdmin);
    DECLARE @b3 bigint=(SELECT BookingId FROM dbo.Bookings WHERE TenantId=@tA AND BookingNumber='BK-1000003');
    INSERT INTO dbo.BookingRooms (BookingId,TenantId,RoomId,RatePerNight,Nights,TotalRoomCharge) VALUES (@b3,@tA,@r301,12000,2,24000);

    /* --- BK-1000004: corporate - Ahmed Raza via ABC agreement, Standard 103 (+13d .. +15d) --- */
    DECLARE @b4In datetime2(0)=DATEADD(DAY,13,@now), @b4Out datetime2(0)=DATEADD(DAY,15,@now);
    INSERT INTO dbo.Bookings (TenantId, BookingNumber, PrimaryGuestId, BookingType, BookingSource, BookingStatus,
        CorporateCustomerId, AgreementId, CheckInDate, CheckOutDate, NumberOfAdults,
        DiscountAmount, TaxAmount, TotalAmount, Notes, CreatedBy)
    VALUES (@tA,'BK-1000004',@gAhmed,1,3,1,@corpABC,@agrABC,@b4In,@b4Out,1,0,500.00,10500,N'Corporate booking - contracted rate applied',@superAdmin);
    DECLARE @b4 bigint=(SELECT BookingId FROM dbo.Bookings WHERE TenantId=@tA AND BookingNumber='BK-1000004');
    INSERT INTO dbo.BookingRooms (BookingId,TenantId,RoomId,RatePerNight,Nights,TotalRoomCharge) VALUES (@b4,@tA,@r103,5000,2,10000);
    INSERT INTO dbo.BookingGuests (BookingId,TenantId,GuestId) VALUES (@b4,@tA,@gAhmed);

    UPDATE dbo.PropertySettings SET BookingCounter=1000004, InvoiceCounter=1 WHERE TenantId=@tA;
END

/* ============================ TENANT B : HOTEL ABC ======================= */
DECLARE @tB bigint;
IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantCode = 'HOTELABC')
    INSERT INTO dbo.Tenants (TenantCode, BusinessName, LegalName, Status, ContactEmail, ContactPhone,
                             Address, City, Country, TimeZone, Currency, CreatedBy)
    VALUES ('HOTELABC', N'Hotel ABC', N'ABC Hotels Ltd', 1,
            N'reception@hotelabc.example', N'+92-51-999888777',
            N'Blue Area', N'Islamabad', N'Pakistan', N'Pakistan Standard Time', 'PKR', @superAdmin);
SELECT @tB = TenantId FROM dbo.Tenants WHERE TenantCode = 'HOTELABC';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'admin@hotelabc.com')
BEGIN
    DECLARE @uB table (UserId bigint);
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy)
    OUTPUT inserted.UserId INTO @uB
    VALUES (@tB, N'Farhan ABC Admin', N'admin@hotelabc.com', N'+92-311-8888888', @superAdmin);
    INSERT INTO dbo.UserRoles SELECT UserId, 2 FROM @uB;
    INSERT INTO dbo.Users (TenantId, FullName, Email, Phone, CreatedBy) VALUES (@tB, N'Hina ABC FD', N'frontdesk@hotelabc.com', N'+92-311-7777777', @superAdmin);
    INSERT INTO dbo.UserRoles VALUES (SCOPE_IDENTITY(), 4);
END

IF NOT EXISTS (SELECT 1 FROM dbo.PropertySettings WHERE TenantId = @tB)
BEGIN
    INSERT INTO dbo.CompanyInformations (TenantId, BusinessName, DisplayName, Address, City, Country, Phone, Email, TaxPercentage, TaxEnabled, CreatedBy)
    VALUES (@tB, N'Hotel ABC', N'Hotel ABC', N'Blue Area', N'Islamabad', N'Pakistan', N'+92-51-999888777', N'reception@hotelabc.example', 0, 0, @superAdmin);
    INSERT INTO dbo.PropertySettings (TenantId, PropertyName, PropertyType, NumberOfFloors, CurrencyCode, TimeZoneId, TaxRatePercent, CreatedBy)
    VALUES (@tB, N'Hotel ABC', N'Hotel', 5, 'PKR', N'Pakistan Standard Time', 0, @superAdmin);
    INSERT INTO dbo.Departments (TenantId, Name) VALUES (@tB,N'Front Desk'),(@tB,N'Housekeeping'),(@tB,N'Management');
    INSERT INTO dbo.RoomTypes (TenantId, Name, Description, MaxOccupancy, BasePrice, ExtraAdultPrice, ExtraChildPrice, NumberOfBeds, BedConfiguration, Amenities, SortOrder, CreatedBy) VALUES
        (@tB, N'Standard', N'Standard hotel room', 2, 6000, 1800, 900, 1, N'1 Queen', N'AC,TV,WiFi', 1, @superAdmin),
        (@tB, N'Deluxe',   N'Deluxe hotel room',   3, 8000, 2200, 1100,1, N'1 King',  N'AC,TV,WiFi,Bathtub', 2, @superAdmin);
    INSERT INTO dbo.Rooms (TenantId, RoomTypeId, RoomNumber, Floor, CreatedBy)
    SELECT @tB, rt.RoomTypeId, r.Num, r.Flr, @superAdmin
    FROM (VALUES (N'Standard',N'101',1),(N'Standard',N'102',1),(N'Deluxe',N'201',2),(N'Deluxe',N'202',2)) r(TypeName,Num,Flr)
    JOIN dbo.RoomTypes rt ON rt.TenantId=@tB AND rt.Name=r.TypeName;
    INSERT INTO dbo.Guests (TenantId, FirstName, LastName, Gender, Nationality, IdType, IdNumber, Phone, City, Country, CreatedBy)
    VALUES (@tB, N'Hassan', N'Raza', 0, N'Pakistani', 0, N'61101-2223334-5', N'+92-345-9991122', N'Islamabad', N'Pakistan', @superAdmin);
END

COMMIT TRAN;
PRINT 'Seed data inserted successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;
GO
