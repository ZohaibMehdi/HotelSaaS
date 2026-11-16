
import React, { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "../api/client";
import type { BookingDetail as BD, Invoice } from "../types";
import { labels, BookingStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";
import { fmtMoney, fmtDateTime } from "@/lib/format";

const statusColor: Record<number, string> = { 0: "amber", 1: "blue", 2: "green", 3: "violet", 4: "red", 5: "slate" };

export function BookingDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { toast } = useToast();
  const apiError = useApiError();
  const [booking, setBooking] = useState<BD | null>(null);
  const [chargeOpen, setChargeOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [chargeForm, setChargeForm] = useState<Record<string, unknown>>({ chargeType: 1, quantity: 1 });
  const [generatedInvoiceId, setGeneratedInvoiceId] = useState<number | null>(null);

  const load = () => api.get(`/bookings/${id}`).then((r) => setBooking(r.data.data));
  useEffect(() => { load(); }, [id]);

  if (!booking) return null;

  const act = async (fn: () => Promise<unknown>, okMsg: string) => {
    try { await fn(); toast("success", okMsg); await load(); }
    catch (e) { toast("error", apiError(e)); }
  };

  const checkOut = async () => {
    try {
      const r = await api.post(`/bookings/${booking.bookingId}/check-out`);
      toast("success", `Checked out. Invoice ${r.data.data.invoiceId} generated.`);
      setGeneratedInvoiceId(r.data.data.invoiceId);
      await load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const addCharge = async () => {
    try {
      await api.post(`/bookings/${booking.bookingId}/charges`, chargeForm);
      toast("success", "Charge added to folio");
      setChargeOpen(false); setChargeForm({ chargeType: 1, quantity: 1 }); await load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const cancel = async () => {
    try {
      await api.post(`/bookings/${booking.bookingId}/cancel`, { reason });
      toast("success", "Booking cancelled"); setCancelOpen(false); await load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const canCheckIn = booking.status === BookingStatus.Confirmed;
  const canCheckOut = booking.status === BookingStatus.CheckedIn;
  const canCharge = booking.status === BookingStatus.CheckedIn || booking.status === BookingStatus.Confirmed || booking.status === BookingStatus.Pending;

  return (
    <>
      <PageHeader title={`Booking ${booking.bookingNumber}`}
        subtitle={`${booking.primaryGuestName} · ${new Date(booking.checkInDate).toLocaleDateString()} → ${new Date(booking.checkOutDate).toLocaleDateString()}`}
        actions={
          <>
            <Badge color={statusColor[booking.status]}>{labels.bookingStatus[booking.status]}</Badge>
            {booking.bookingType === 1 && <Badge color="violet">Corporate</Badge>}
          </>
        } />

      <div className="grid lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="card p-4">
            <h3 className="font-semibold text-sm text-slate-700 mb-3">Rooms &amp; Tariff</h3>
            <table className="min-w-full">
              <thead><tr><th className="th">Room</th><th className="th">Type</th><th className="th text-right">Rate/Night</th><th className="th text-right">Nights</th><th className="th text-right">Total</th></tr></thead>
              <tbody>
                {booking.rooms.map((r) => (
                  <tr key={r.bookingRoomId}>
                    <td className="td font-semibold">{r.roomNumber}</td>
                    <td className="td">{r.roomTypeName}</td>
                    <td className="td text-right">{fmtMoney(r.ratePerNight)}</td>
                    <td className="td text-right">{r.nights}</td>
                    <td className="td text-right">{fmtMoney(r.totalRoomCharge)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="card p-4">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-semibold text-sm text-slate-700">Guest Folio Charges</h3>
              {canCharge && <button className="btn-secondary !h-8 !px-3 !text-xs" onClick={() => setChargeOpen(true)}>+ Add Charge</button>}
            </div>
            {booking.charges.length === 0 ? (
              <p className="text-xs text-slate-400 py-3">No charges posted yet.</p>
            ) : (
              <table className="min-w-full">
                <thead><tr><th className="th">Description</th><th className="th">Type</th><th className="th">Posted</th><th className="th text-right">Qty</th><th className="th text-right">Unit</th><th className="th text-right">Total</th></tr></thead>
                <tbody>
                  {booking.charges.map((c) => (
                    <tr key={c.chargeId}>
                      <td className="td">{c.description}</td>
                      <td className="td"><Badge color={c.chargeType === 0 ? "blue" : c.chargeType === 1 ? "amber" : "slate"}>{labels.chargeType[c.chargeType]}</Badge></td>
                      <td className="td text-xs text-slate-400">{fmtDateTime(c.chargeDate)}</td>
                      <td className="td text-right">{c.quantity}</td>
                      <td className="td text-right">{fmtMoney(c.unitPrice)}</td>
                      <td className="td text-right font-medium">{fmtMoney(c.totalAmount)}</td>
                    </tr>
                  ))}
                  <tr><td colSpan={5} className="td text-right font-semibold text-slate-500">Folio total</td>
                    <td className="td text-right font-bold">{fmtMoney(booking.chargesTotal)}</td></tr>
                </tbody>
              </table>
            )}
          </div>
        </div>

        <div className="space-y-5">
          <div className="card p-4 space-y-2 text-xs">
            <h3 className="font-semibold text-sm text-slate-700 mb-2">Stay Information</h3>
            <Row k="Guest" v={<Link className="text-primary hover:underline" to="/guests">{booking.primaryGuestName}</Link>} />
            <Row k="Adults / Children" v={`${booking.numberOfAdults} / ${booking.numberOfChildren}`} />
            <Row k="Actual check-in" v={fmtDateTime(booking.actualCheckIn)} />
            <Row k="Actual check-out" v={fmtDateTime(booking.actualCheckOut)} />
            {booking.corporateCustomerName && <Row k="Company" v={booking.corporateCustomerName} />}
            {booking.agreementNumber && <Row k="Agreement" v={booking.agreementNumber} />}
            <Row k="Estimated tax" v={fmtMoney(booking.taxAmount)} />
            <Row k="Discount" v={fmtMoney(booking.discountAmount)} />
            <Row k="Deposit" v={fmtMoney(booking.depositAmount)} />
            {booking.specialRequests && <Row k="Requests" v={booking.specialRequests} />}
            {booking.cancellationReason && <Row k="Cancellation reason" v={booking.cancellationReason} />}
            <div className="pt-2 border-t border-slate-100 flex justify-between font-semibold text-sm">
              <span>Estimated total</span><span>{fmtMoney(booking.totalAmount)}</span>
            </div>
          </div>

          <div className="card p-4 flex flex-col gap-2">
            <h3 className="font-semibold text-sm text-slate-700">Actions</h3>
            {canCheckIn && <button className="btn-success" onClick={() => act(() => api.post(`/bookings/${booking.bookingId}/check-in`), "Guest checked in — room is now Occupied")}>Check In</button>}
            {canCheckOut && <button className="btn-primary" onClick={checkOut}>Check Out &amp; Generate Invoice</button>}
            {(booking.status === BookingStatus.Confirmed || booking.status === BookingStatus.Pending) && (
              <button className="btn-danger" onClick={() => setCancelOpen(true)}>Cancel Booking</button>
            )}
            {booking.status === BookingStatus.CheckedOut && generatedInvoiceId !== null && (
              <Link className="btn-secondary text-center" to={`/invoices/${generatedInvoiceId}`}>View Generated Invoice</Link>
            )}
            {booking.bookingId && booking.status >= BookingStatus.Cancelled && (
              <p className="text-[11px] text-slate-400">This booking is closed; actions are restricted.</p>
            )}
            <button className="btn-secondary" onClick={() => navigate("/bookings")}>Back to list</button>
          </div>
        </div>
      </div>

      <Modal open={chargeOpen} title="Add Folio Charge" onClose={() => setChargeOpen(false)}>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="label">Charge Type *</label>
            <select className="input" value={chargeForm.chargeType as number}
              onChange={(e) => setChargeForm({ ...chargeForm, chargeType: Number(e.target.value) })}>
              {[1, 2, 3, 4, 5, 6, 7].map((t) => <option key={t} value={t}>{labels.chargeType[t]}</option>)}
            </select>
          </div>
          <div>
            <label className="label">Quantity *</label>
            <input type="number" step="0.01" min="0.01" className="input" value={(chargeForm.quantity as number) ?? 1}
              onChange={(e) => setChargeForm({ ...chargeForm, quantity: Number(e.target.value) })} />
          </div>
          <div>
            <label className="label">Unit Price *</label>
            <input type="number" step="0.01" min="0" className="input" value={(chargeForm.unitPrice as number) ?? ""}
              onChange={(e) => setChargeForm({ ...chargeForm, unitPrice: Number(e.target.value) })} />
          </div>
          <div>
            <label className="label">Discount</label>
            <input type="number" min="0" className="input" value={(chargeForm.discountAmount as number) ?? ""}
              onChange={(e) => setChargeForm({ ...chargeForm, discountAmount: Number(e.target.value) })} />
          </div>
          <div className="col-span-2">
            <label className="label">Description *</label>
            <input className="input" value={(chargeForm.description as string) ?? ""}
              onChange={(e) => setChargeForm({ ...chargeForm, description: e.target.value })}
              placeholder="e.g. Breakfast buffet" />
          </div>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setChargeOpen(false)}>Cancel</button>
          <button className="btn-primary" onClick={addCharge}>Add Charge</button>
        </div>
      </Modal>

      <Modal open={cancelOpen} title="Cancel Booking" onClose={() => setCancelOpen(false)}>
        <label className="label">Cancellation reason *</label>
        <textarea className="input" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setCancelOpen(false)}>Keep Booking</button>
          <button className="btn-danger" onClick={cancel}>Cancel Booking</button>
        </div>
      </Modal>
    </>
  );
}

function Row({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-2">
      <span className="text-slate-400 shrink-0">{k}</span>
      <span className="text-slate-600 text-right font-medium">{v}</span>
    </div>
  );
}

export type { Invoice };



