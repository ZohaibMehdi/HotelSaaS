namespace HotelSaaS.Domain;

// Enum numeric values are part of the database contract (see database/01_schema.sql comments).

public enum TenantStatus { Pending = 0, Active = 1, Suspended = 2, Inactive = 3 }

public enum RoomStatus { Available = 0, Reserved = 1, Occupied = 2, Cleaning = 3, Maintenance = 4, OutOfService = 5 }

public enum HousekeepingStatus { Clean = 0, Dirty = 1, Cleaning = 2, Inspected = 3, Maintenance = 4 }

public enum BookingSource { WalkIn = 0, Phone = 1, Website = 2, Corporate = 3, TravelAgent = 4, Ota = 5, Other = 6 }

public enum BookingStatus { Pending = 0, Confirmed = 1, CheckedIn = 2, CheckedOut = 3, Cancelled = 4, NoShow = 5 }

public enum BookingType { Individual = 0, Corporate = 1 }

public enum Gender { Male = 0, Female = 1, Other = 2 }

public enum IdType { Cnic = 0, Passport = 1, NationalId = 2, DrivingLicense = 3, Other = 4 }

public enum ChargeType
{
    Room = 0, Food = 1, Laundry = 2, ExtraBed = 3, AdditionalGuest = 4,
    Transport = 5, MiniBar = 6, Other = 7, Adjustment = 8
}

public enum InvoiceType { Guest = 0, Corporate = 1 }

public enum InvoiceStatus { Draft = 0, Issued = 1, PartiallyPaid = 2, Paid = 3, Cancelled = 4 }

public enum PaymentMethod { Cash = 0, BankTransfer = 1, Card = 2, Online = 3, Other = 4 }

public enum CorporateStatus { Inactive = 0, Active = 1, Suspended = 2 }

public enum AgreementStatus { Draft = 0, Active = 1, Expired = 2, Suspended = 3, Cancelled = 4 }

public enum EmploymentStatus { Probation = 0, Active = 1, OnLeave = 2, Suspended = 3, Terminated = 4 }

public enum PayrollRunStatus { Draft = 0, Finalized = 1, Paid = 2 }

public enum HousekeepingTaskType { Cleaning = 0, Inspection = 1, Maintenance = 2 }

public enum HousekeepingTaskStatus { Pending = 0, InProgress = 1, Completed = 2, Cancelled = 3 }

public enum NotificationChannel { InApp = 0, Email = 1, Sms = 2, WhatsApp = 3 }

public enum NotificationStatus { Pending = 0, Sent = 1, Read = 2, Failed = 3 }

public enum NotificationType
{
    Info = 0, BookingConfirmed = 1, BookingCancelled = 2, CheckInReminder = 3,
    PaymentReceipt = 4, OutstandingBalance = 5, AgreementExpiring = 6,
    PayrollCompleted = 7, PasswordReset = 8, System = 9
}

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string FrontDesk = "FrontDesk";
    public const string Finance = "Finance";
    public const string Hr = "HR";
    public const string Housekeeping = "Housekeeping";

    /// <summary>Roles that operate inside a tenant context.</summary>
    public static readonly string[] TenantRoles =
        { TenantAdmin, Manager, FrontDesk, Finance, Hr, Housekeeping };
}
