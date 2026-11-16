import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "../api/client";
import type { Invoice, PaymentDto } from "../types";
import { labels, InvoiceStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";

import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";
import { fmtMoney, fmtDate } from "@/lib/format";

const statusColor: Record<number, string> = { 0: "slate", 1: "blue", 2: "amber", 3: "green", 4: "red" };

export function InvoicesPage() {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "100" });
    if (statusFilter !== "") qs.set("status", statusFilter);
    if (search) qs.set("search", search);
    api.get(`/invoices?${qs}`).then((r) => setInvoices(r.data.data.items));
  };
  useEffect(() => { load(); }, [statusFilter]); // eslint-disable-line

  return (
    <>
      <PageHeader title="Billing" subtitle="Guest and corporate invoices" />
      <div className="flex gap-2 mb-3">
        <form onSubmit={(e) => { e.preventDefault(); load(); }}>
          <input className="input w-56" placeholder="Invoice # / guest / company..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </form>
        <select className="input w-44" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All statuses</option>
          {labels.invoiceStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
        </select>
      </div>

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Invoice #</th><th className="th">Billed To</th><th className="th">Type</th><th className="th">Date</th><th className="th text-right">Total</th><th className="th text-right">Paid</th><th className="th text-right">Balance</th><th className="th">Status</th></tr></thead>
          <tbody>
            {invoices.map((i) => (
              <tr key={i.invoiceId} className="hover:bg-slate-50">
                <td className="td font-mono text-xs font-semibold">
                  <Link to={`/invoices/${i.invoiceId}`} className="text-primary hover:underline">{i.invoiceNumber}</Link>
                </td>
                <td className="td">{i.corporateCustomerName ?? i.guestName ?? "-"}</td>
                <td className="td">{i.type === 1 ? <Badge color="violet">Corporate</Badge> : <span className="text-xs text-slate-400">Guest</span>}</td>
                <td className="td text-xs">{fmtDate(i.invoiceDate)}</td>
                <td className="td text-right">{fmtMoney(i.totalAmount)}</td>
                <td className="td text-right">{fmtMoney(i.paidAmount)}</td>
                <td className={`td text-right font-medium ${i.balanceDue > 0 ? "text-red-600" : ""}`}>{fmtMoney(i.balanceDue)}</td>
                <td className="td"><Badge color={statusColor[i.status]}>{labels.invoiceStatus[i.status]}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function InvoiceDetailPage() {
  const { id } = useParams();
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [payOpen, setPayOpen] = useState(false);
  const [form, setForm] = useState<Record<string, unknown>>({ method: 0 });
  const [busy, setBusy] = useState(false);

  const load = () => api.get(`/invoices/${id}`).then((r) => setInvoice(r.data.data));
  useEffect(() => { load(); }, [id]);
  if (!invoice) return null;

  return (
    <>
      <PageHeader title={`Invoice ${invoice.invoiceNumber}`}
        subtitle={`${invoice.corporateCustomerName ?? invoice.guestName ?? ""} · ${fmtDate(invoice.invoiceDate)}`}
        actions={<Badge color={statusColor[invoice.status]}>{labels.invoiceStatus[invoice.status]}</Badge>} />

      <div className="grid lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 card p-6 print:shadow-none">
          {/* Printable invoice body */}
          <div className="border-b border-slate-200 pb-4 mb-4">
            <h2 className="text-lg font-bold text-slate-800">{invoice.bookingNumber ? `Stay folio · ${invoice.bookingNumber}` : "Invoice"}</h2>
            <p className="text-xs text-slate-400 mt-0.5">Issued {new Date(invoice.invoiceDate).toLocaleString()}{invoice.dueDate ? ` · Due ${invoice.dueDate}` : ""}</p>
          </div>
          <table className="min-w-full mb-5">
            <thead><tr><th className="th">Description</th><th className="th text-right">Qty</th><th className="th text-right">Unit Price</th><th className="th text-right">Tax</th><th className="th text-right">Total</th></tr></thead>
            <tbody>
              {invoice.items.map((it, i) => (
                <tr key={i}>
                  <td className="td">{it.description}</td>
                  <td className="td text-right">{it.quantity}</td>
                  <td className="td text-right">{fmtMoney(it.unitPrice)}</td>
                  <td className="td text-right">{fmtMoney(it.taxAmount)}</td>
                  <td className="td text-right">{fmtMoney(it.totalAmount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex justify-end">
            <dl className="text-sm space-y-1 w-64">
              <Line k="Subtotal" v={fmtMoney(invoice.subtotal)} />
              <Line k="Discounts" v={fmtMoney(invoice.discountTotal)} />
              <Line k="Tax" v={fmtMoney(invoice.taxTotal)} />
              <Line k="Total" v={fmtMoney(invoice.totalAmount)} bold />
              <Line k="Paid" v={fmtMoney(invoice.paidAmount)} />
              <Line k="Balance Due" v={fmtMoney(invoice.balanceDue)} bold highlight={invoice.balanceDue > 0 && invoice.status !== InvoiceStatus.Cancelled} />
            </dl>
          </div>
        </div>

        <div className="space-y-5">
          <div className="card p-4">
            <h3 className="font-semibold text-sm text-slate-700 mb-3">Payments</h3>
            {(invoice.payments ?? []).length === 0 ? (
              <p className="text-xs text-slate-400">No payments recorded.</p>
            ) : (
              <ul className="divide-y divide-slate-100 text-xs">
                {invoice.payments.map((p: PaymentDto) => (
                  <li key={p.paymentId} className="py-2 flex justify-between gap-2">
                    <span>
                      <span className="font-medium">{fmtMoney(p.amount)}</span>
                      <span className="text-slate-400 block">{labels.paymentMethod[p.method]}{p.transactionReference ? ` · ${p.transactionReference}` : ""}</span>
                    </span>
                    <span className="text-slate-400 whitespace-nowrap">{fmtDate(p.paymentDate)}</span>
                  </li>
                ))}
              </ul>
            )}
            {invoice.balanceDue > 0 && invoice.status !== InvoiceStatus.Cancelled && (
              <button className="btn-primary w-full mt-3" onClick={() => setPayOpen(true)}>Record Payment</button>
            )}
          </div>

          <button className="btn-secondary w-full" onClick={() => window.print()}>Print / Save PDF</button>
        </div>
      </div>

      <PaymentModal open={payOpen} onClose={() => setPayOpen(false)} balance={invoice.balanceDue}
        busy={busy} form={form} setForm={setForm}
        onDone={() => { setPayOpen(false); load(); }} invoiceId={invoice.invoiceId} setBusy={setBusy} />
    </>
  );
}

function PaymentModal({ open, onClose, balance, onDone, invoiceId, form, setForm, busy, setBusy }: {
  open: boolean; onClose: () => void; balance: number; onDone: () => void;
  invoiceId: number; form: Record<string, unknown>; setForm: (f: Record<string, unknown>) => void;
  busy: boolean; setBusy: (b: boolean) => void;
}) {
  const { toast } = useToast();
  const apiError = useApiError();
  const pay = async (allowOverpayment = false) => {
    setBusy(true);
    try {
      await api.post(`/invoices/${invoiceId}/payments?allowOverpayment=${allowOverpayment}`, form);
      toast("success", "Payment recorded");
      onDone();
    } catch (e) {
      const msg = apiError(e);
      if (msg.includes("exceeds the outstanding balance") &&
        confirm(`${msg}\n\nAllow overpayment anyway?`)) { setBusy(false); await pay(true); return; }
      toast("error", msg);
    } finally { setBusy(false); }
  };
  return (
    <Modal open={open} title={`Record Payment — Balance ${balance.toLocaleString()}`} onClose={onClose}>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="label">Amount *</label>
          <input type="number" step="0.01" min="0.01" className="input" defaultValue={balance}
            onChange={(e) => setForm({ ...form, amount: Number(e.target.value) })} />
        </div>
        <div>
          <label className="label">Method</label>
          <select className="input" onChange={(e) => setForm({ ...form, method: Number(e.target.value) })}>
            {labels.paymentMethod.map((m, i) => <option key={i} value={i}>{m}</option>)}
          </select>
        </div>
        <div className="col-span-2">
          <label className="label">Transaction Reference</label>
          <input className="input" placeholder="e.g. TXN-12345 or CASH-001"
            onChange={(e) => setForm({ ...form, transactionReference: e.target.value })} />
        </div>
        <div className="col-span-2">
          <label className="label">Notes</label>
          <input className="input" onChange={(e) => setForm({ ...form, notes: e.target.value })} />
        </div>
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <button className="btn-secondary" onClick={onClose}>Cancel</button>
        <button className="btn-primary" onClick={() => pay()} disabled={busy}>{busy ? "Saving..." : "Record"}</button>
      </div>
    </Modal>
  );
}

function Line({ k, v, bold, highlight }: { k: string; v: string; bold?: boolean; highlight?: boolean }) {
  return (
    <div className={`flex justify-between ${bold ? "font-bold border-t border-slate-200 pt-1" : ""}`}>
      <dt className={highlight ? "text-red-600" : "text-slate-500"}>{k}</dt>
      <dd className={highlight ? "text-red-600 font-semibold" : bold ? "text-slate-800" : "text-slate-600"}>{v}</dd>
    </div>
  );
}

export function PaymentsPage() {
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  useEffect(() => { api.get("/payments").then((r) => setPayments(r.data.data)); }, []);
  return (
    <>
      <PageHeader title="Payments" subtitle="Recent payment receipts across all invoices" />
      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Date</th><th className="th">Amount</th><th className="th">Method</th><th className="th">Reference</th><th className="th">Notes</th></tr></thead>
          <tbody>
            {payments.map((p) => (
              <tr key={p.paymentId} className="hover:bg-slate-50">
                <td className="td text-xs">{fmtDate(p.paymentDate)}</td>
                <td className="td font-semibold">{fmtMoney(p.amount)}</td>
                <td className="td"><Badge>{labels.paymentMethod[p.method]}</Badge></td>
                <td className="td font-mono text-xs">{p.transactionReference ?? "-"}</td>
                <td className="td text-xs text-slate-400">{p.notes ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

