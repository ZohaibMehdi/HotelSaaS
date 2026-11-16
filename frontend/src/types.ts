// API type contracts mirroring HotelSaaS DTOs. Enum values are numeric.

export interface UserSummary {
  userId: number;
  tenantId: number | null;
  tenantName: string | null;
  fullName: string;
  email: string;
  phone: string | null;
  roles: string[];
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: UserSummary;
}

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export interface Tenant {
  tenantId: number; tenantCode: string; businessName: string; legalName: string | null;
  status: number; contactEmail: string; contactPhone: string | null; address: string | null;
  city: string | null; country: string | null; timeZone: string; currency: string;
  createdAt: string; isActive: boolean;
}

export interface RoomType {
  roomTypeId: number; name: string; description: string | null; maxOccupancy: number;
  basePrice: number; extraAdultPrice: number; extraChildPrice: number; numberOfBeds: number;
  bedConfiguration: string | null; amenities: string | null; isActive: boolean;
}

export interface Room {
  roomId: number; roomTypeId: number; roomTypeName: string; roomNumber: string; floor: number | null;
  status: RoomStatus; housekeepingStatus: HkStatus; notes: string | null; isActive: boolean;
}

export enum RoomStatus { Available = 0, Reserved = 1, Occupied = 2, Cleaning = 3, Maintenance = 4, OutOfService = 5 }
export enum HkStatus { Clean = 0, Dirty = 1, Cleaning = 2, Inspected = 3, Maintenance = 4 }

export interface Guest {
  guestId: number; firstName: string; lastName: string | null; gender: number | null;
  dateOfBirth: string | null; nationality: string | null; idType: number; idNumber: string | null;
  phone: string | null; email: string | null; address: string | null; city: string | null;
  country: string | null; notes: string | null; isActive: boolean; createdAt: string;
}

export interface BookingRoom { bookingRoomId: number; roomId: number; roomNumber: string; roomTypeName: string; ratePerNight: number; nights: number; totalRoomCharge: number }
export interface Charge { chargeId: number; chargeType: number; description: string; chargeDate: string; quantity: number; unitPrice: number; discountAmount: number; taxAmount: number; totalAmount: number }

export interface BookingListItem {
  bookingId: number; bookingNumber: string; guestName: string; status: BookingStatus;
  type: BookingType; checkInDate: string; checkOutDate: string; totalAmount: number; rooms: string;
}
export enum BookingStatus { Pending = 0, Confirmed = 1, CheckedIn = 2, CheckedOut = 3, Cancelled = 4, NoShow = 5 }
export enum BookingType { Individual = 0, Corporate = 1 }

export interface BookingDetail {
  bookingId: number; tenantId: number; bookingNumber: string; primaryGuestId: number; primaryGuestName: string;
  bookingType: BookingType; bookingSource: number; status: BookingStatus;
  corporateCustomerId: number | null; corporateCustomerName: string | null;
  agreementId: number | null; agreementNumber: string | null;
  checkInDate: string; checkOutDate: string; actualCheckIn: string | null; actualCheckOut: string | null;
  numberOfAdults: number; numberOfChildren: number; discountAmount: number; taxAmount: number;
  depositAmount: number; totalAmount: number; specialRequests: string | null; notes: string | null;
  rooms: BookingRoom[]; charges: Charge[]; chargesTotal: number; cancellationReason: string | null;
}

export interface AvailabilityRoom { roomId: number; roomNumber: string; roomTypeName: string; roomTypeId: number; ratePerNight: number; isAvailable: boolean }

export interface InvoiceItemDto { description: string; chargeDate: string | null; quantity: number; unitPrice: number; discountAmount: number; taxAmount: number; totalAmount: number }
export interface PaymentDto { paymentId: number; amount: number; method: number; transactionReference: string | null; paymentDate: string; notes: string | null; isReversed: boolean }

export interface Invoice {
  invoiceId: number; invoiceNumber: string; bookingId: number | null; bookingNumber: string | null;
  guestId: number | null; guestName: string | null; corporateCustomerId: number | null; corporateCustomerName: string | null;
  type: number; invoiceDate: string; dueDate: string | null; subtotal: number; discountTotal: number;
  taxTotal: number; totalAmount: number; paidAmount: number; balanceDue: number; status: InvoiceStatus;
  notes: string | null; items: InvoiceItemDto[]; payments: PaymentDto[];
}
export enum InvoiceStatus { Draft = 0, Issued = 1, PartiallyPaid = 2, Paid = 3, Cancelled = 4 }

export interface CorporateCustomer {
  corporateCustomerId: number; companyName: string; registrationNumber: string | null; taxNumber: string | null;
  contactPerson: string | null; phone: string | null; email: string | null; address: string | null;
  billingAddress: string | null; status: CorporateStatus; notes: string | null; isActive: boolean;
}
export enum CorporateStatus { Inactive = 0, Active = 1, Suspended = 2 }

export interface AgreementRate { agreementRateId: number; roomTypeId: number; roomTypeName: string; contractedRate: number; extraAdultRate: number; extraChildRate: number; includesBreakfast: boolean; taxApplicable: boolean }
export interface Agreement {
  agreementId: number; corporateCustomerId: number; companyName: string; agreementNumber: string;
  startDate: string; endDate: string; status: AgreementStatus; paymentTermsDays: number;
  creditLimit: number | null; includesBreakfast: boolean; specialFacilities: string | null;
  cancellationRules: string | null; notes: string | null; rates: AgreementRate[];
}
export enum AgreementStatus { Draft = 0, Active = 1, Expired = 2, Suspended = 3, Cancelled = 4 }

export interface Employee {
  employeeId: number; employeeCode: string; firstName: string; lastName: string | null; cnic: string | null;
  phone: string | null; email: string | null; departmentId: number | null; departmentName: string | null;
  designation: string | null; joiningDate: string; employmentStatus: EmploymentStatus; basicSalary: number;
  bankName: string | null; bankAccountNumber: string | null; emergencyContactName: string | null;
  emergencyContactPhone: string | null; terminationDate: string | null; isActive: boolean;
}
export enum EmploymentStatus { Probation = 0, Active = 1, OnLeave = 2, Suspended = 3, Terminated = 4 }

export interface PayrollItemDto {
  payrollItemId: number; employeeId: number; employeeName: string; employeeCode: string; department: string | null;
  basicSalary: number; allowances: number; overtimeAmount: number; bonusAmount: number; deductions: number;
  advanceDeduction: number; loanDeduction: number; absenceDeduction: number; grossSalary: number; netSalary: number; remarks: string | null;
}
export interface PayrollRun {
  payrollRunId: number; payrollMonth: string; periodStart: string; periodEnd: string; status: PayrollStatus;
  totalGross: number; totalDeductions: number; totalNet: number; finalizedAt: string | null; paidAt: string | null;
  notes: string | null; items: PayrollItemDto[];
}
export enum PayrollStatus { Draft = 0, Finalized = 1, Paid = 2 }

export interface HkTask {
  taskId: number; roomId: number; roomNumber: string; bookingId: number | null; bookingNumber: string | null;
  taskType: number; status: HkTaskStatus; assignedToEmployeeId: number | null; assignedToName: string | null;
  scheduledFor: string; completedAt: string | null; notes: string | null;
}
export enum HkTaskStatus { Pending = 0, InProgress = 1, Completed = 2, Cancelled = 3 }

export interface TenantDashboard {
  totalRooms: number; availableRooms: number; occupiedRooms: number; reservedRooms: number;
  cleaningRooms: number; maintenanceRooms: number; todaysArrivals: number; todaysDepartures: number;
  currentGuests: number; todaysRevenue: number; outstandingBalance: number; pendingBookings: number;
  occupancyPercent: number;
  arrivalsToday: ArrivalDeparture[]; departuresToday: ArrivalDeparture[];
}
export interface ArrivalDeparture { bookingId: number; bookingNumber: string; guestName: string; roomNumbers: string; time: string; status: number }

export interface PlatformDashboard {
  totalTenants: number; activeTenants: number; inactiveTenants: number; suspendedTenants: number;
  totalUsers: number; activeUsers: number; totalRooms: number; totalBookings: number;
  recentTenants: { tenantId: number; tenantCode: string; businessName: string; status: number; createdAt: string }[];
  dbLatencyMs: number;
}

export interface AuditLog {
  auditLogId: number; tenantId: number | null; userId: number | null; userName: string | null;
  action: string; entityName: string; entityId: string | null; oldValues: string | null;
  newValues: string | null; ipAddress: string | null; correlationId: string | null; createdAt: string;
}

export interface CompanyInformation {
  companyInformationId: number; businessName: string; displayName: string | null; legalName: string | null;
  registrationNumber: string | null; taxNumber: string | null; address: string | null; city: string | null;
  country: string | null; postalCode: string | null; phone: string | null; email: string | null;
  website: string | null; logoPath: string | null; invoiceHeader: string | null; invoiceFooter: string | null;
  taxPercentage: number; taxEnabled: boolean;
}

export interface PropertySettings {
  propertySettingsId: number; propertyName: string; propertyType: string; numberOfFloors: number | null;
  checkInTime: string; checkOutTime: string; currencyCode: string; timeZoneId: string;
  taxRatePercent: number; taxInclusive: boolean; invoicePrefix: string; bookingPrefix: string;
  defaultBreakfastIncluded: boolean; bookingPolicyText: string | null; cancellationPolicyText: string | null; paymentPolicyText: string | null;
}

export interface DepartmentDto { departmentId: number; name: string }

// ---- label helpers ----
export const labels = {
  roomStatus: ["Available", "Reserved", "Occupied", "Cleaning", "Maintenance", "Out of Service"],
  hkStatus: ["Clean", "Dirty", "Cleaning", "Inspected", "Maintenance"],
  bookingStatus: ["Pending", "Confirmed", "Checked In", "Checked Out", "Cancelled", "No-Show"],
  bookingSource: ["Walk-in", "Phone", "Website", "Corporate", "Travel Agent", "OTA", "Other"],
  bookingType: ["Individual", "Corporate"],
  chargeType: ["Room", "Food", "Laundry", "Extra Bed", "Additional Guest", "Transport", "Mini Bar", "Other", "Adjustment"],
  invoiceStatus: ["Draft", "Issued", "Partially Paid", "Paid", "Cancelled"],
  paymentMethod: ["Cash", "Bank Transfer", "Card", "Online", "Other"],
  corporateStatus: ["Inactive", "Active", "Suspended"],
  agreementStatus: ["Draft", "Active", "Expired", "Susp.", "Cancelled"],
  employmentStatus: ["Probation", "Active", "On Leave", "Suspended", "Terminated"],
  payrollStatus: ["Draft", "Finalized", "Paid"],
  hkTaskStatus: ["Pending", "In Progress", "Completed", "Cancelled"],
  hkTaskType: ["Cleaning", "Inspection", "Maintenance"],
  tenantStatus: ["Pending", "Active", "Suspended", "Inactive"],
  idTypes: ["CNIC", "Passport", "National ID", "Driving License", "Other"]
};


