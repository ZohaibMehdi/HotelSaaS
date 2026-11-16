import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import { ToastProvider } from "./context/ToastContext";
import { AuthLayout, DashboardLayout } from "./layouts/Layouts";
import { RequireAuth, RequireRoles } from "./routes/guards";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { TenantsPage, TenantDetailPage } from "./pages/TenantsPage";
import { UsersPage } from "./pages/UsersPage";
import { RoomTypesPage, RoomsPage } from "./pages/InventoryPages";
import { GuestsPage } from "./pages/GuestsPage";
import { BookingsPage } from "./pages/BookingsPage";
import { NewBookingPage } from "./pages/NewBookingPage";
import { BookingDetailPage } from "./pages/BookingDetailPage";
import { InvoicesPage, InvoiceDetailPage, PaymentsPage } from "./pages/BillingPages";
import { CorporateCustomersPage, AgreementsPage } from "./pages/B2BPages";
import { EmployeesPage, PayrollPage } from "./pages/WorkforcePages";
import { HousekeepingPage } from "./pages/HousekeepingPage";
import { ReportsPage } from "./pages/ReportsPage";
import { AuditLogsPage } from "./pages/AuditLogsPage";
import { CompanyInfoPage } from "./pages/SettingsPage";

const STAFF = ["SuperAdmin", "TenantAdmin", "Manager", "FrontDesk"];

export default function App() {
  return (
    <AuthProvider>
      <ToastProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AuthLayout />}>
              <Route path="/login" element={<LoginPage />} />
            </Route>

            <Route element={<RequireAuth><DashboardLayout /></RequireAuth>}>
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/" element={<Navigate to="/dashboard" replace />} />

              {/* Platform administration */}
              <Route path="/tenants" element={<RequireRoles roles={["SuperAdmin"]}><TenantsPage /></RequireRoles>} />
              <Route path="/tenants/:id" element={<TenantDetailPage />} />
              <Route path="/users" element={<RequireRoles roles={["SuperAdmin", "TenantAdmin"]}><UsersPage /></RequireRoles>} />
              <Route path="/audit-logs" element={<RequireRoles roles={["SuperAdmin", "TenantAdmin", "Manager"]}><AuditLogsPage /></RequireRoles>} />

              {/* Inventory */}
              <Route path="/room-types" element={<RoomTypesPage />} />
              <Route path="/rooms" element={<RoomsPage />} />

              {/* Operations */}
              <Route path="/guests" element={<GuestsPage />} />
              <Route path="/bookings" element={<BookingsPage />} />
              <Route path="/bookings/new" element={<RequireRoles roles={STAFF}><NewBookingPage /></RequireRoles>} />
              <Route path="/bookings/:id" element={<BookingDetailPage />} />
              <Route path="/housekeeping" element={<HousekeepingPage />} />

              {/* Billing */}
              <Route path="/invoices" element={<InvoicesPage />} />
              <Route path="/invoices/:id" element={<InvoiceDetailPage />} />
              <Route path="/payments" element={<PaymentsPage />} />

              {/* B2B */}
              <Route path="/corporate-customers" element={<CorporateCustomersPage />} />
              <Route path="/agreements" element={<AgreementsPage />} />

              {/* Workforce */}
              <Route path="/employees" element={<RequireRoles roles={["SuperAdmin", "TenantAdmin", "Manager", "HR", "Finance"]}><EmployeesPage /></RequireRoles>} />
              <Route path="/payroll" element={<RequireRoles roles={["SuperAdmin", "TenantAdmin", "HR", "Finance"]}><PayrollPage /></RequireRoles>} />

              {/* Insights & config */}
              <Route path="/reports" element={<RequireRoles roles={["SuperAdmin", "TenantAdmin", "Manager", "Finance"]}><ReportsPage /></RequireRoles>} />
              <Route path="/company-info" element={<CompanyInfoPage />} />

              <Route path="*" element={<Navigate to="/dashboard" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ToastProvider>
    </AuthProvider>
  );
}

