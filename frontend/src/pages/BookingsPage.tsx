import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { BookingListItem } from "../types";
import { labels, BookingType, BookingStatus } from "../types";
import { PageHeader, Badge } from "../components/ui";

const statusColor: Record<number, string> = { 0: "amber", 1: "blue", 2: "green", 3: "violet", 4: "red", 5: "slate" };

export function BookingsPage() {
  const [bookings, setBookings] = useState<BookingListItem[]>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "100" });
    if (statusFilter !== "") qs.set("status", statusFilter);
    if (search) qs.set("search", search);
    api.get(`/bookings?${qs}`).then((r) => setBookings(r.data.data.items));
  };
  useEffect(() => { load(); }, [statusFilter]); // eslint-disable-line

  return (
    <>
      <PageHeader title="Bookings" subtitle="Reservations and stays"
        actions={<Link to="/bookings/new" className="btn-primary">+ New Reservation</Link>} />

      <div className="flex gap-2 mb-3 flex-wrap">
        <form onSubmit={(e) => { e.preventDefault(); load(); }}>
          <input className="input w-56" placeholder="Booking no / guest name..." value={search}
            onChange={(e) => setSearch(e.target.value)} />
        </form>
        <select className="input w-44" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All statuses</option>
          {labels.bookingStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
        </select>
      </div>

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Booking #</th><th className="th">Guest</th><th className="th">Rooms</th><th className="th">Type</th><th className="th">Check-in</th><th className="th">Check-out</th><th className="th">Status</th><th className="th text-right">Total</th></tr></thead>
          <tbody>
            {bookings.map((b) => (
              <tr key={b.bookingId} className="hover:bg-slate-50">
                <td className="td font-mono text-xs font-semibold">
                  <Link to={`/bookings/${b.bookingId}`} className="text-primary hover:underline">{b.bookingNumber}</Link>
                </td>
                <td className="td">{b.guestName}</td>
                <td className="td">{b.rooms}</td>
                <td className="td">{b.type === BookingType.Corporate ? <Badge color="violet">Corporate</Badge> : <span className="text-xs text-slate-400">{labels.bookingType[b.type]}</span>}</td>
                <td className="td text-xs">{new Date(b.checkInDate).toLocaleDateString()}</td>
                <td className="td text-xs">{new Date(b.checkOutDate).toLocaleDateString()}</td>
                <td className="td"><Badge color={statusColor[b.status as BookingStatus]}>{labels.bookingStatus[b.status]}</Badge></td>
                <td className="td text-right font-medium">{b.totalAmount.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

