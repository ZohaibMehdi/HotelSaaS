
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import { useAuth } from "../context/AuthContext";
import type { PlatformDashboard, TenantDashboard, ArrivalDeparture } from "../types";
import { labels } from "../types";
import { PageHeader, StatCard, Spinner, Badge, EmptyState } from "../components/ui";
import { fmtMoney, fmtDateTime } from "@/lib/format";

export function DashboardPage() {
  const { user } = useAuth();
  const isSuper = user?.roles.includes("SuperAdmin");
  const [tenantData, setTenantData] = useState<TenantDashboard | null>(null);
  const [platformData, setPlatformData] = useState<PlatformDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    api.get(isSuper ? "/dashboard/platform" : "/dashboard/tenant")
      .then((r) => (isSuper ? setPlatformData(r.data.data as PlatformDashboard) : setTenantData(r.data.data as TenantDashboard)))
      .catch((e) => setError(e.apiMessage ?? "Failed to load dashboard"));
  }, [isSuper]);

  if (error) return <div className="text-sm text-red-600">{error}</div>;
  if (isSuper) return platformData ? <PlatformView d={platformData} /> : <Spinner />;
  return tenantData ? <TenantView d={tenantData} /> : <Spinner />;
}

function TenantView({ d }: { d: TenantDashboard }) {
  const statusColor: Record<number, string> = { 0: "green", 1: "blue", 2: "amber", 3: "violet", 4: "red", 5: "slate" };
  return (
    <>
      <PageHeader title="Operations Dashboard" subtitle="Live property overview" />
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="Total Rooms" value={d.totalRooms} />
        <StatCard label="Available" value={d.availableRooms} hint={`${d.occupancyPercent}% occupancy`} />
        <StatCard label="Occupied" value={d.occupiedRooms} />
        <StatCard label="Reserved" value={d.reservedRooms} />
        <StatCard label="Cleaning / Maint." value={`${d.cleaningRooms} / ${d.maintenanceRooms}`} />
        <StatCard label="Today's Arrivals" value={d.todaysArrivals} />
        <StatCard label="Today's Departures" value={d.todaysDepartures} />
        <StatCard label="In-house Guests" value={d.currentGuests} />
        <StatCard label="Today's Revenue" value={fmtMoney(d.todaysRevenue)} />
        <StatCard label="Outstanding Balance" value={fmtMoney(d.outstandingBalance)} />
        <StatCard label="Pending Bookings" value={d.pendingBookings} />
      </div>

      <div className="grid md:grid-cols-2 gap-5 mt-6">
        <ArrivalsList title="Arrivals Today" rows={d.arrivalsToday} empty="No arrivals scheduled today." />
        <ArrivalsList title="Departures Today" rows={d.departuresToday} empty="No departures scheduled today." />
      </div>

      <div className="card p-5 mt-6">
        <h3 className="font-semibold text-slate-700 text-sm mb-3">Room Status Breakdown</h3>
        {([0, 1, 2, 3, 4] as const).map((s) => (
          <Bar key={s} label={labels.roomStatus[s]} value={
            s === 0 ? d.availableRooms : s === 1 ? d.reservedRooms :
            s === 2 ? d.occupiedRooms : s === 3 ? d.cleaningRooms : d.maintenanceRooms
          } total={Math.max(1, d.totalRooms)} color={statusColor[s]} />
        ))}
      </div>
    </>
  );
}

function Bar({ label, value, total, color }: { label: string; value: number; total: number; color: string }) {
  return (
    <div className="flex items-center gap-3 mb-2">
      <span className="w-28 text-xs text-slate-500 shrink-0">{label}</span>
      <div className="flex-1 h-3 bg-slate-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color === "green" ? "bg-emerald-500" : color === "blue" ? "bg-blue-500" : color === "amber" ? "bg-amber-500" : color === "violet" ? "bg-violet-500" : "bg-slate-400"}`}
          style={{ width: `${(value * 100) / total}%` }} />
      </div>
      <span className="w-8 text-xs font-medium text-right">{value}</span>
    </div>
  );
}

function ArrivalsList({ title, rows, empty }: { title: string; rows: ArrivalDeparture[]; empty: string }) {
  return (
    <div className="card p-4">
      <h3 className="font-semibold text-slate-700 text-sm mb-3">{title}</h3>
      {rows.length === 0 ? <EmptyState message={empty} /> : (
        <ul className="divide-y divide-slate-100">
          {rows.map((r) => (
            <li key={r.bookingId} className="py-2 flex items-center justify-between gap-2">
              <Link to={`/bookings/${r.bookingId}`} className="text-sm hover:text-primary">
                <span className="font-medium">{r.guestName}</span>
                <span className="text-slate-400"> · {r.roomNumbers}</span>
              </Link>
              <span className="text-xs text-slate-400 flex items-center gap-2">
                {fmtDateTime(r.time)}
                <Badge color={r.status === 1 ? "blue" : r.status === 2 ? "amber" : "slate"}>{labels.bookingStatus[r.status]}</Badge>
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function PlatformView({ d }: { d: PlatformDashboard }) {
  return (
    <>
      <PageHeader title="Platform Dashboard" subtitle="SaaS-wide statistics" />
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="Total Tenants" value={d.totalTenants} hint={`${d.activeTenants} active`} />
        <StatCard label="Inactive / Suspended" value={`${d.inactiveTenants} / ${d.suspendedTenants}`} />
        <StatCard label="Total Users" value={d.totalUsers} hint={`${d.activeUsers} active`} />
        <StatCard label="Total Rooms" value={d.totalRooms} />
        <StatCard label="Total Bookings" value={d.totalBookings} />
        <StatCard label="DB Latency" value={`${d.dbLatencyMs} ms`} hint="health probe" />
      </div>

      <div className="card p-5 mt-6 overflow-x-auto">
        <h3 className="font-semibold text-slate-700 text-sm mb-3">Recent Tenant Onboarding</h3>
        <table className="min-w-full">
          <thead><tr><th className="th">Code</th><th className="th">Business</th><th className="th">Status</th><th className="th">Onboarded</th></tr></thead>
          <tbody>
            {d.recentTenants.map((t) => (
              <tr key={t.tenantId}>
                <td className="td font-mono text-xs">{t.tenantCode}</td>
                <td className="td"><Link className="hover:text-primary" to={`/tenants/${t.tenantId}`}>{t.businessName}</Link></td>
                <td className="td"><Badge color={t.status === 1 ? "green" : t.status === 2 ? "amber" : "red"}>{labels.tenantStatus[t.status]}</Badge></td>
                <td className="td text-xs text-slate-400">{fmtDateTime(t.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}



