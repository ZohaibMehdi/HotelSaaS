import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { Button } from "@/components/ui/button";

interface NavItem { to: string; label: string; roles?: string[] }

const platformNav: NavItem[] = [
  { to: "/dashboard", label: "Dashboard" },
  { to: "/tenants", label: "Tenants" },
  { to: "/users", label: "Users" },
  { to: "/audit-logs", label: "Audit Logs" }
];

const tenantNav: NavItem[] = [
  { to: "/dashboard", label: "Dashboard" },
  { to: "/bookings", label: "Bookings" },
  { to: "/guests", label: "Guests" },
  { to: "/rooms", label: "Rooms" },
  { to: "/room-types", label: "Room Types" },
  { to: "/corporate-customers", label: "Companies", roles: ["TenantAdmin", "Manager", "FrontDesk", "Finance"] },
  { to: "/agreements", label: "B2B Agreements", roles: ["TenantAdmin", "Manager", "FrontDesk", "Finance"] },
  { to: "/invoices", label: "Billing" },
  { to: "/employees", label: "Employees" },
  { to: "/payroll", label: "Payroll", roles: ["TenantAdmin", "HR", "Finance"] },
  { to: "/housekeeping", label: "Housekeeping", roles: ["TenantAdmin", "Manager", "FrontDesk", "Housekeeping"] },
  { to: "/reports", label: "Reports", roles: ["TenantAdmin", "Manager", "Finance"] },
  { to: "/company-info", label: "Settings" }
];

function initials(name?: string | null) {
  if (!name) return "?";
  return name.split(" ").filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join("");
}

export function DashboardLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const isSuper = user?.roles.includes("SuperAdmin");
  const nav = (isSuper ? platformNav : tenantNav).filter(
    (n) => !n.roles || (user?.roles.some((r) => n.roles!.includes(r)))
  );

  const current = nav.find(
    (n) => location.pathname === n.to || location.pathname.startsWith(n.to + "/")
  );
  const pageTitle = current?.label ??
    (location.pathname.startsWith("/bookings") ? "Bookings"
      : location.pathname.startsWith("/invoices") ? "Billing"
      : "HotelSaaS");

  const handleLogout = () => { logout(); navigate("/login"); };

  return (
    <div className="flex min-h-svh w-full">
      {/* ------------------------------ Sidebar ------------------------------ */}
      <aside className="sticky top-0 hidden h-svh w-60 shrink-0 flex-col border-r bg-background md:flex print:hidden">
        <div className="flex h-14 items-center gap-2 border-b px-5">
          <div className="flex h-6 w-6 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.2} className="h-3.5 w-3.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6" />
            </svg>
          </div>
          <span className="text-sm font-semibold tracking-tight">HotelSaaS</span>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3">
          {!isSuper && (
            <div className="mb-2 rounded-md bg-muted px-3 py-2">
              <div className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">Property</div>
              <div className="truncate text-sm font-medium">{user?.tenantName}</div>
            </div>
          )}
          {nav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.to === "/dashboard"}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 text-sm transition-colors ${
                  isActive
                    ? "bg-secondary font-medium text-secondary-foreground shadow-sm"
                    : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                }`
              }
            >
              {n.label}
            </NavLink>
          ))}
        </nav>

        <div className="border-t p-3">
          <div className="flex items-center gap-3 rounded-md px-2 py-2">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
              {initials(user?.fullName)}
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-medium">{user?.fullName}</div>
              <div className="truncate text-xs text-muted-foreground">{user?.roles[0]}</div>
            </div>
          </div>
          <Button variant="ghost" className="mt-1 w-full justify-start px-3 text-muted-foreground">
            Sign out
          </Button>
        </div>
      </aside>

      {/* ------------------------------- Main -------------------------------- */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-4 border-b bg-background px-6 print:hidden">
          <div className="md:hidden text-sm font-semibold">HotelSaaS</div>
          <div className="hidden md:block text-sm font-medium">{pageTitle}</div>
          <div className="ml-auto flex items-center gap-3">
            <span className="hidden rounded-full border px-2.5 py-0.5 text-xs text-muted-foreground sm:inline-flex">
              {isSuper ? "Platform" : user?.tenantName}
            </span>
            <Button variant="outline" size="sm" onClick={handleLogout}>
              Sign out
            </Button>
          </div>
        </header>

        <main className="flex-1 overflow-x-hidden">
          <div className="mx-auto max-w-7xl space-y-6 p-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}

export function AuthLayout() {
  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Outlet />
    </div>
  );
}


