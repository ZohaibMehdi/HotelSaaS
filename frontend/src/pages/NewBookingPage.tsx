import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";
import type { AvailabilityRoom, RoomType, Guest, CorporateCustomer } from "../types";
import { labels } from "../types";
import { PageHeader, Badge } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";

export function NewBookingPage() {
  const navigate = useNavigate();
  const { toast } = useToast();
  const apiError = useApiError();

  const [fromDate, setFromDate] = useState(new Date(Date.now() + 864e5).toISOString().slice(0, 10));
  const [toDate, setToDate] = useState(new Date(Date.now() + 2 * 864e5).toISOString().slice(0, 10));
  const [roomTypeId, setRoomTypeId] = useState("");
  const [types, setTypes] = useState<RoomType[]>([]);
  const [guests, setGuests] = useState<Guest[]>([]);
  const [corps, setCorps] = useState<CorporateCustomer[]>([]);

  const [corporate, setCorporate] = useState(false);
  const [corpId, setCorpId] = useState("");
  const [agreements, setAgreements] = useState<{ agreementId: number; agreementNumber: string; status: number }[]>([]);
  const [agrId, setAgrId] = useState("");

  const [rooms, setRooms] = useState<AvailabilityRoom[]>([]);
  const [selectedRoom, setSelectedRoom] = useState<number | null>(null);
  const [guestId, setGuestId] = useState("");
  const [adults, setAdults] = useState(1);
  const [children, setChildren] = useState(0);
  const [source, setSource] = useState("0");
  const [discount, setDiscount] = useState(0);
  const [deposit, setDeposit] = useState(0);
  const [requests, setRequests] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get("/room-types").then((r) => setTypes(r.data.data));
    api.get("/guests?page=1&pageSize=500").then((r) => setGuests(r.data.data.items));
    if (corporate) {
      api.get("/corporate-customers?status=1&pageSize=200").then((r) => setCorps(r.data.data.items));
      setAgreements([]);
      setAgrId("");
    }
  }, [corporate]);

  useEffect(() => {
    if (!corpId) return;
    api.get(`/b2b-agreements?corporateCustomerId=${corpId}&status=1`).then((r) => {
      setAgreements(r.data.data);
      setAgrId(r.data.data[0]?.agreementId?.toString() ?? "");
    });
  }, [corpId]);

  const searchAvailability = async () => {
    try {
      const qs = new URLSearchParams({ from: `${fromDate}T14:00:00`, to: `${toDate}T12:00:00` });
      if (roomTypeId) qs.set("roomTypeId", roomTypeId);
      if (corporate && agrId) qs.set("agreementId", agrId);
      const r = await api.get(`/bookings/availability?${qs}`);
      setRooms(r.data.data);
    } catch (e) { toast("error", apiError(e)); }
  };

  const submit = async () => {
    if (!selectedRoom || !guestId) { toast("error", "Select a room and a guest."); return; }
    setBusy(true);
    try {
      const resp = await api.post("/bookings", {
        guestId: Number(guestId),
        bookingSource: corporate ? 3 : Number(source),
        bookingType: corporate ? 1 : 0,
        corporateCustomerId: corporate ? Number(corpId) : null,
        agreementId: corporate ? Number(agrId) : null,
        checkInDate: `${fromDate}T14:00:00`,
        checkOutDate: `${toDate}T12:00:00`,
        roomIds: [selectedRoom],
        numberOfAdults: adults,
        numberOfChildren: children,
        discountAmount: discount,
        depositAmount: deposit,
        specialRequests: requests || null
      });
      toast("success", `Reservation ${resp.data.data.bookingNumber} created`);
      navigate(`/bookings/${resp.data.data.bookingId}`);
    } catch (e) { toast("error", apiError(e)); } finally { setBusy(false); }
  };

  const selected = rooms.find((r) => r.roomId === selectedRoom);

  return (
    <>
      <PageHeader title="New Reservation" subtitle="Check availability, pick a room, attach the guest" />

      <div className="grid lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="card p-4">
            <h3 className="font-semibold text-sm text-slate-700 mb-3">1 · Stay Dates</h3>
            <div className="grid sm:grid-cols-3 gap-4">
              <div>
                <label className="label">Check-in</label>
                <input type="date" className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
              </div>
              <div>
                <label className="label">Check-out</label>
                <input type="date" className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
              </div>
              <div>
                <label className="label">Room Type</label>
                <select className="input" value={roomTypeId} onChange={(e) => setRoomTypeId(e.target.value)}>
                  <option value="">Any type</option>
                  {types.map((t) => <option key={t.roomTypeId} value={t.roomTypeId}>{t.name}</option>)}
                </select>
              </div>
            </div>
            <button className="btn-primary mt-4" onClick={searchAvailability}>Search Availability</button>
          </div>

          {rooms.length > 0 && (
            <div className="card p-4">
              <h3 className="font-semibold text-sm text-slate-700 mb-3">2 · Select Room</h3>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                {rooms.map((r) => (
                  <button key={r.roomId} disabled={!r.isAvailable}
                    onClick={() => setSelectedRoom(r.roomId)}
                    className={`p-3 rounded-md border text-left transition-colors ${
                      !r.isAvailable ? "border-slate-100 bg-slate-50 opacity-50 cursor-not-allowed"
                      : selectedRoom === r.roomId ? "border-primary bg-muted ring-1 ring-ring"
                      : "border-slate-200 hover:border-zinc-400 bg-white"}`}>
                    <div className="font-bold">{r.roomNumber}</div>
                    <div className="text-[11px] text-slate-500">{r.roomTypeName}</div>
                    <div className={`text-xs font-semibold mt-1 ${corporate ? "text-violet-700" : "text-emerald-700"}`}>
                      {r.ratePerNight.toLocaleString()} /night
                      {corporate && <span className="ml-1 text-[10px]">(contract)</span>}
                    </div>
                    {!r.isAvailable && <div className="text-[10px] text-red-500 mt-0.5">Booked for these dates</div>}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="card p-4">
            <h3 className="font-semibold text-sm text-slate-700 mb-3">3 · Guest &amp; Details</h3>
            <div className="grid sm:grid-cols-2 gap-4">
              <div>
                <label className="label">Primary Guest *</label>
                <select className="input" value={guestId} onChange={(e) => setGuestId(e.target.value)}>
                  <option value="">-- select guest --</option>
                  {guests.filter((g) => g.isActive).map((g) => (
                    <option key={g.guestId} value={g.guestId}>{g.firstName} {g.lastName} ({g.phone})</option>
                  ))}
                </select>
                <p className="text-[11px] text-slate-400 mt-1">New guests can be added from the Guests module.</p>
              </div>
              <div>
                <label className="label">Source</label>
                <select className="input" value={source} disabled={corporate} onChange={(e) => setSource(e.target.value)}>
                  {labels.bookingSource.map((s, i) => <option key={i} value={i}>{s}</option>)}
                </select>
              </div>
              <div><label className="label">Adults</label><input type="number" min={1} className="input" value={adults} onChange={(e) => setAdults(Number(e.target.value))} /></div>
              <div><label className="label">Children</label><input type="number" min={0} className="input" value={children} onChange={(e) => setChildren(Number(e.target.value))} /></div>
              <div><label className="label">Discount Amount</label><input type="number" min={0} className="input" value={discount} onChange={(e) => setDiscount(Number(e.target.value))} /></div>
              <div><label className="label">Deposit Received</label><input type="number" min={0} className="input" value={deposit} onChange={(e) => setDeposit(Number(e.target.value))} /></div>
              <div className="sm:col-span-2">
                <label className="label">Special Requests</label>
                <textarea rows={2} className="input" value={requests} onChange={(e) => setRequests(e.target.value)} />
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-5">
          <div className="card p-4">
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={corporate} onChange={(e) => setCorporate(e.target.checked)} className="accent-zinc-900" />
              <span className="text-sm font-semibold text-slate-700">Corporate Booking (B2B)</span>
            </label>
            {corporate && (
              <div className="mt-3 space-y-3">
                <select className="input" value={corpId} onChange={(e) => setCorpId(e.target.value)}>
                  <option value="">-- company --</option>
                  {corps.map((c) => <option key={c.corporateCustomerId} value={c.corporateCustomerId}>{c.companyName}</option>)}
                </select>
                <select className="input" value={agrId} onChange={(e) => setAgrId(e.target.value)}>
                  <option value="">-- active agreement --</option>
                  {agreements.map((a) => <option key={a.agreementId} value={a.agreementId}>{a.agreementNumber}</option>)}
                </select>
                <Badge color="violet">Contracted rates apply automatically</Badge>
              </div>
            )}
          </div>

          <div className="card p-4 sticky top-4">
            <h3 className="font-semibold text-sm text-slate-700 mb-2">Summary</h3>
            <dl className="text-xs space-y-1.5 text-slate-500">
              <div className="flex justify-between"><dt>Stay</dt><dd>{fromDate} → {toDate}</dd></div>
              <div className="flex justify-between"><dt>Room</dt><dd>{selected ? `${selected.roomNumber} (${selected.roomTypeName})` : "-"}</dd></div>
              <div className="flex justify-between"><dt>Nightly rate</dt><dd>{selected ? selected.ratePerNight.toLocaleString() : "-"}</dd></div>
              <div className="flex justify-between"><dt>Type</dt><dd>{corporate ? "Corporate" : labels.bookingSource[Number(source)]}</dd></div>
            </dl>
            <button className="btn-primary w-full mt-4" onClick={submit} disabled={busy || !selectedRoom || !guestId}>
              {busy ? "Creating..." : "Create Reservation"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

