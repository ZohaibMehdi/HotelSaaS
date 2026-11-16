import React, { useEffect, useState } from "react";
import { api } from "../api/client";
import { PageHeader, StatCard } from "../components/ui";

interface OccupancyPoint { date: string; totalRooms: number; occupiedRooms: number; occupancyPercent: number }
interface RevenuePoint { date: string; payments: number; invoiced: number }
interface BySource { source: string; count: number }
interface Receivable { invoiceId: number; invoiceNumber: string; billedTo: string; isCorporate: boolean; balanceDue: number; dueDate: string | null }
interface GuestStat { guestId: number; name: string; stayCount: number; totalSpent: number }

const iso = (d: Date) => d.toISOString().slice(0, 10);

export function ReportsPage() {
  const [tab, setTab] = useState<"occupancy" | "revenue" | "receivables" | "bookings" | "guests">("occupancy");
  const [from, setFrom] = useState(iso(new Date(Date.now() - 30 * 864e5)));
  const [to, setTo] = useState(iso(new Date()));
  const [data, setData] = useState<unknown>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    const f = `${from}T00:00:00`, t = `${to}T23:59:59`;
    const url =
      tab === "occupancy" ? `/reports/occupancy?from=${f}&to=${t}` :
      tab === "revenue" ? `/reports/revenue?from=${f}&to=${t}` :
      tab === "receivables" ? `/reports/receivables` :
      tab === "bookings" ? `/reports/bookings-by-source` :
      `/reports/guests?top=15`;
    api.get(url)
      .then((r) => setData(r.data.data))
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, [tab, from, to]);

  useEffect(() => {
    if (tab === "guests") return;
    // extra datasets for revenue tab
  }, [tab]);

  return (
    <>
      <PageHeader title="Reports" subtitle="Operational and financial insights" />

      <div className="flex flex-wrap items-end gap-3 mb-4">
        <div className="flex rounded-md overflow-hidden border border-slate-300 bg-white text-sm">
          {(["occupancy", "revenue", "receivables", "bookings", "guests"] as const).map((t) => (
            <button key={t}
              onClick={() => setTab(t)}
              className={`px-4 py-2 capitalize ${tab === t ? "bg-primary text-white font-medium" : "hover:bg-slate-50"}`}>
              {t}
            </button>
          ))}
        </div>
        {(tab === "occupancy" || tab === "revenue") && (
          <>
            <input type="date" className="input w-auto" value={from} onChange={(e) => setFrom(e.target.value)} />
            <span className="text-xs text-slate-400 pb-2.5">to</span>
            <input type="date" className="input w-auto" value={to} onChange={(e) => setTo(e.target.value)} />
          </>
        )}
      </div>

      {loading && <div className="text-sm text-slate-400 py-6">Loading report...</div>}

      {!loading && tab === "occupancy" && (
        <div>
          <OccupancyBars rows={(data as OccupancyPoint[]) ?? []} />
        </div>
      )}
      {!loading && tab === "revenue" && (
        <div className="space-y-5">
          <RevenueTable rows={(data as RevenuePoint[]) ?? []} />
          <RoomTypeRevenueCard from={from} to={to} />
        </div>
      )}
      {!loading && tab === "receivables" && <ReceivablesTable rows={(data as Receivable[]) ?? []} />}
      {!loading && tab === "bookings" && <BookingsBySource rows={(data as BySource[]) ?? []} />}
      {!loading && tab === "guests" && <GuestStats rows={(data as GuestStat[]) ?? []} />}
    </>
  );
}

function OccupancyBars({ rows }: { rows: OccupancyPoint[] }) {
  if (!rows.length) return null;
  return (
    <div className="card p-5">
      <h3 className="font-semibold text-sm mb-4">Daily Occupancy %</h3>
      <div className="flex items-end gap-[3px] h-48">
        {rows.map((r) => (
          <div key={r.date} className="flex-1 group relative" title={`${r.date}: ${r.occupiedRooms}/${r.totalRooms} (${r.occupancyPercent}%)`}>
            <div className="w-full bg-zinc-800 rounded-t group-hover:bg-primary transition-colors"
              style={{ height: `${Math.max(2, r.occupancyPercent)}%` }} />
          </div>
        ))}
      </div>
      <div className="flex justify-between text-[10px] text-slate-400 mt-2">
        <span>{rows[0]?.date}</span><span>{rows[rows.length - 1]?.date}</span>
      </div>
    </div>
  );
}

function RevenueTable({ rows }: { rows: RevenuePoint[] }) {
  const totalPayments = rows.reduce((a, r) => a + r.payments, 0);
  const totalInvoiced = rows.reduce((a, r) => a + r.invoiced, 0);
  return (
    <>
      <div className="grid grid-cols-2 gap-4">
        <StatCard label="Cash Received (period)" value={totalPayments.toLocaleString()} />
        <StatCard label="Invoiced (period)" value={totalInvoiced.toLocaleString()} />
      </div>
      <div className="card mt-4 overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Date</th><th className="th text-right">Payments Received</th><th className="th text-right">Invoiced</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.date}>
                <td className="td text-xs">{new Date(r.date).toLocaleDateString()}</td>
                <td className="td text-right">{r.payments.toLocaleString()}</td>
                <td className="td text-right">{r.invoiced.toLocaleString()}</td>
              </tr>
            ))}
            {rows.length === 0 && <tr><td colSpan={3} className="td text-center text-xs text-slate-400 py-6">No activity in this period.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}

function RoomTypeRevenueCard({ from, to }: { from: string; to: string }) {
  const [rows, setRows] = useState<{ roomType: string; amount: number }[]>([]);
  React.useEffect(() => {
    api.get(`/reports/revenue-by-room-type?from=${from}T00:00:00&to=${to}T23:59:59`).then((r) => setRows(r.data.data));
  }, [from, to]);
  const max = Math.max(1, ...rows.map((r) => r.amount));
  return (
    <div className="card p-5">
      <h3 className="font-semibold text-sm mb-3">Room Revenue by Room Type</h3>
      {rows.map((r) => (
        <div key={r.roomType} className="flex items-center gap-3 mb-2">
          <span className="w-28 text-xs text-slate-500">{r.roomType}</span>
          <div className="flex-1 h-3 bg-slate-100 rounded-full overflow-hidden">
            <div className="h-full bg-violet-500 rounded-full" style={{ width: `${(r.amount * 100) / max}%` }} />
          </div>
          <span className="w-24 text-right text-xs font-medium">{r.amount.toLocaleString()}</span>
        </div>
      ))}
    </div>
  );
}

function ReceivablesTable({ rows }: { rows: Receivable[] }) {
  return (
    <div className="card overflow-x-auto">
      <table className="min-w-full">
        <thead><tr><th className="th">Invoice #</th><th className="th">Billed To</th><th className="th">Due Date</th><th className="th text-right">Balance Due</th></tr></thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.invoiceId} className="hover:bg-slate-50">
              <td className="td font-mono text-xs font-semibold">{r.invoiceNumber}</td>
              <td className="td">{r.billedTo}{r.isCorporate && <span className="ml-1.5 text-[10px] text-violet-600 font-semibold">B2B</span>}</td>
              <td className="td text-xs">{r.dueDate ?? "-"}</td>
              <td className="td text-right font-semibold text-red-600">{r.balanceDue.toLocaleString()}</td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={4} className="td text-center text-xs text-slate-400 py-6">No outstanding receivables.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}

function BookingsBySource({ rows }: { rows: BySource[] }) {
  const max = Math.max(1, ...rows.map((r) => r.count));
  return (
    <div className="card p-5 max-w-xl">
      <h3 className="font-semibold text-sm mb-3">Bookings by Source</h3>
      {rows.map((r) => (
        <div key={r.source} className="flex items-center gap-3 mb-2">
          <span className="w-28 text-xs text-slate-500">{r.source}</span>
          <div className="flex-1 h-3 bg-slate-100 rounded-full overflow-hidden">
            <div className="h-full bg-blue-500 rounded-full" style={{ width: `${(r.count * 100) / max}%` }} />
          </div>
          <span className="w-8 text-right text-xs font-medium">{r.count}</span>
        </div>
      ))}
    </div>
  );
}

function GuestStats({ rows }: { rows: GuestStat[] }) {
  return (
    <div className="card overflow-x-auto max-w-2xl">
      <table className="min-w-full">
        <thead><tr><th className="th">Guest</th><th className="th text-right">Stays</th><th className="th text-right">Total Spent</th></tr></thead>
        <tbody>
          {rows.map((g) => (
            <tr key={g.guestId} className="hover:bg-slate-50">
              <td className="td font-medium">{g.name}</td>
              <td className="td text-right">{g.stayCount}</td>
              <td className="td text-right font-medium">{g.totalSpent.toLocaleString()}</td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={3} className="td text-center text-xs text-slate-400 py-6">No completed stays yet.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}


