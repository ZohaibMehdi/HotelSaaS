import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { CorporateCustomer, Agreement, RoomType } from "../types";
import { labels, AgreementStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";

const agrColor: Record<number, string> = { 0: "slate", 1: "green", 2: "amber", 3: "red", 4: "slate" };

export function CorporateCustomersPage() {
  const [customers, setCustomers] = useState<CorporateCustomer[]>([]);
  const [search, setSearch] = useState("");
  const [edit, setEdit] = useState<Record<string, unknown> | null>(null);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "200" });
    if (search) qs.set("search", search);
    api.get(`/corporate-customers?${qs}`).then((r) => setCustomers(r.data.data.items));
  };
  useEffect(() => { load(); }, [search]); // eslint-disable-line

  const save = async () => {
    try {
      if (edit!.corporateCustomerId) await api.put(`/corporate-customers/${edit!.corporateCustomerId}`, edit);
      else await api.post("/corporate-customers", edit);
      toast("success", "Corporate customer saved");
      setEdit(null); load();
    } catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Corporate Customers" subtitle="B2B companies with negotiated terms"
        actions={<button className="btn-primary" onClick={() => setEdit({ status: 1 })}>+ New Company</button>} />

      <input className="input max-w-xs mb-3" placeholder="Search company / contact..." value={search} onChange={(e) => setSearch(e.target.value)} />
      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Company</th><th className="th">Contact</th><th className="th">Phone</th><th className="th">Status</th><th className="th">Actions</th></tr></thead>
          <tbody>
            {customers.map((c) => (
              <tr key={c.corporateCustomerId} className={`hover:bg-slate-50 ${!c.isActive ? "opacity-50" : ""}`}>
                <td className="td font-medium">{c.companyName}</td>
                <td className="td text-xs">{c.contactPerson ?? "-"}<span className="text-slate-400 block">{c.email ?? ""}</span></td>
                <td className="td text-xs">{c.phone ?? "-"}</td>
                <td className="td"><Badge color={c.status === 1 ? "green" : c.status === 2 ? "red" : "slate"}>{labels.corporateStatus[c.status]}</Badge></td>
                <td className="td"><button className="btn-xs-secondary" onClick={() => setEdit({ ...c })}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal open={!!edit} title={edit?.corporateCustomerId ? "Edit Company" : "New Corporate Customer"} onClose={() => setEdit(null)} wide>
        <div className="grid grid-cols-2 gap-4">
          {([
            ["companyName", "Company Name *"], ["registrationNumber", "Registration No"],
            ["taxNumber", "Tax Number"], ["contactPerson", "Contact Person"],
            ["phone", "Phone"], ["email", "Email"]
          ] as [string, string][]).map(([n, l]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input className="input" value={(edit?.[n] as string) ?? ""} onChange={(e) => setEdit({ ...edit!, [n]: e.target.value })} />
            </div>
          ))}
          <div>
            <label className="label">Status</label>
            <select className="input" value={String(edit?.status ?? 1)} onChange={(e) => setEdit({ ...edit!, status: Number(e.target.value) })}>
              {labels.corporateStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
            </select>
          </div>
          <div className="col-span-2"><label className="label">Address</label><input className="input" value={(edit?.address as string) ?? ""} onChange={(e) => setEdit({ ...edit!, address: e.target.value })} /></div>
          <div className="col-span-2"><label className="label">Billing Address</label><input className="input" value={(edit?.billingAddress as string) ?? ""} onChange={(e) => setEdit({ ...edit!, billingAddress: e.target.value })} /></div>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setEdit(null)}>Cancel</button>
          <button className="btn-primary" onClick={save}>Save</button>
        </div>
      </Modal>
    </>
  );
}

interface RateRow { roomTypeId: number; contractedRate: number; includesBreakfast: boolean; taxApplicable: boolean }

export function AgreementsPage() {
  const [agreements, setAgreements] = useState<Agreement[]>([]);
  const [types, setTypes] = useState<RoomType[]>([]);
  const [customers, setCustomers] = useState<CorporateCustomer[]>([]);
  const [edit, setEdit] = useState<Record<string, unknown> | null>(null);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => Promise.all([
    api.get("/b2b-agreements").then((r) => setAgreements(r.data.data)),
    api.get("/room-types").then((r) => setTypes(r.data.data)),
    api.get("/corporate-customers?pageSize=200").then((r) => setCustomers(r.data.data.items))
  ]);
  useEffect(() => { load(); }, []); // eslint-disable-line

  const newAgreement = () => {
    setEdit({
      agreementNumber: `AGR-${new Date().getFullYear()}-`,
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date(Date.now() + 365 * 864e5).toISOString().slice(0, 10),
      status: AgreementStatus.Active, paymentTermsDays: 30,
      rates: types.map((t) => ({ roomTypeId: t.roomTypeId, contractedRate: t.basePrice, includesBreakfast: true, taxApplicable: true }))
    });
  };

  const save = async () => {
    try {
      if (edit!.agreementId) await api.put(`/b2b-agreements/${edit!.agreementId}`, edit);
      else await api.post("/b2b-agreements", edit);
      toast("success", "Agreement saved");
      setEdit(null); load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const setStatus = async (a: Agreement, s: number) => {
    try { await api.post(`/b2b-agreements/${a.agreementId}/status/${s}`); load(); }
    catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="B2B Agreements" subtitle="Contracted rates per company and room type"
        actions={<button className="btn-primary" onClick={newAgreement}>+ New Agreement</button>} />

      <div className="space-y-3">
        {agreements.map((a) => (
          <div key={a.agreementId} className="card p-4">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div>
                <span className="font-semibold">{a.agreementNumber}</span>
                <span className="text-slate-500"> · {a.companyName}</span>{" "}
                <Badge color={agrColor[a.status]}>{labels.agreementStatus[a.status]}</Badge>
                <span className="text-xs text-slate-400 block mt-0.5">
                  {a.startDate} → {a.endDate} · Terms {a.paymentTermsDays}d
                  {a.creditLimit ? ` · Credit ${a.creditLimit.toLocaleString()}` : ""}
                  {a.includesBreakfast ? " · Breakfast included" : ""}
                </span>
              </div>
              <div className="flex gap-1.5">
                {a.status === AgreementStatus.Active && <button className="btn-xs-secondary" onClick={() => setStatus(a, 3)}>Suspend</button>}
                {(a.status === AgreementStatus.Suspended || a.status === AgreementStatus.Draft) && <button className="btn-xs-success" onClick={() => setStatus(a, 1)}>Activate</button>}
                <button className="btn-xs-secondary"
                  onClick={() => setEdit({
                    ...a,
                    rates: a.rates.map((r) => ({ roomTypeId: r.roomTypeId, contractedRate: r.contractedRate, includesBreakfast: r.includesBreakfast, taxApplicable: r.taxApplicable }))
                  })}>Edit Rates</button>
              </div>
            </div>
            <div className="flex flex-wrap gap-2 mt-3">
              {a.rates.map((r) => (
                <div key={r.agreementRateId} className="rounded-md bg-slate-50 border border-slate-100 px-3 py-1.5 text-xs">
                  <span className="font-medium text-slate-600">{r.roomTypeName}</span>{" "}
                  <span className="font-bold text-primary">{r.contractedRate.toLocaleString()}</span>
                  {r.includesBreakfast && <span className="text-emerald-600 ml-1">+ breakfast</span>}
                </div>
              ))}
            </div>
          </div>
        ))}
        {agreements.length === 0 && <p className="text-sm text-slate-400 card p-6">No agreements yet.</p>}
      </div>

      <Modal open={!!edit} title={edit?.agreementId ? `Edit ${String(edit.agreementNumber)}` : "New B2B Agreement"} onClose={() => setEdit(null)} wide>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
          <div>
            <label className="label">Agreement Number *</label>
            <input className="input" placeholder="AGR-2026-001" maxLength={40}
              disabled={!!edit?.agreementId}
              value={(edit?.agreementNumber as string) ?? ""}
              onChange={(e) => setEdit({ ...edit!, agreementNumber: e.target.value })} />
          </div>
          <div className="col-span-2">
            <label className="label">Company *</label>
            <select className="input" value={String(edit?.corporateCustomerId ?? "")}
              disabled={!!edit?.agreementId}
              onChange={(e) => setEdit({ ...edit!, corporateCustomerId: Number(e.target.value) })}>
              <option value="">-- company --</option>
              {customers.map((c) => <option key={c.corporateCustomerId} value={c.corporateCustomerId}>{c.companyName}</option>)}
            </select>
          </div>
          <div><label className="label">Start *</label><input type="date" className="input" value={(edit?.startDate as string) ?? ""} onChange={(e) => setEdit({ ...edit!, startDate: e.target.value })} /></div>
          <div><label className="label">End *</label><input type="date" className="input" value={(edit?.endDate as string) ?? ""} onChange={(e) => setEdit({ ...edit!, endDate: e.target.value })} /></div>
          <div>
            <label className="label">Payment Terms (days)</label>
            <input type="number" min="0" className="input" value={(edit?.paymentTermsDays as number) ?? 30} onChange={(e) => setEdit({ ...edit!, paymentTermsDays: Number(e.target.value) })} />
          </div>
          <div>
            <label className="label">Credit Limit</label>
            <input type="number" min="0" className="input" value={(edit?.creditLimit as number) ?? ""} onChange={(e) => setEdit({ ...edit!, creditLimit: e.target.value === "" ? null : Number(e.target.value) })} />
          </div>
          <div>
            <label className="label">Status</label>
            <select className="input" value={String(edit?.status ?? 0)} onChange={(e) => setEdit({ ...edit!, status: Number(e.target.value) })}>
              {[AgreementStatus.Draft, AgreementStatus.Active, AgreementStatus.Suspended].map((s) =>
                <option key={s} value={s}>{labels.agreementStatus[s]}</option>)}
            </select>
          </div>
          <div className="flex items-end pb-2.5">
            <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" className="accent-zinc-900" checked={!!edit?.includesBreakfast} onChange={(e) => setEdit({ ...edit!, includesBreakfast: e.target.checked })} /> Breakfast included</label>
          </div>
        </div>

        <h4 className="font-semibold text-sm text-slate-700 mb-2">Contracted Room Rates *</h4>
        <table className="min-w-full card mb-1">
          <thead><tr><th className="th">Room Type</th><th className="th w-36">Rate/Night</th><th className="th">Breakfast incl.</th><th className="th">Tax applies</th></tr></thead>
          <tbody>
            {((edit?.rates ?? []) as RateRow[]).map((r, i) => (
              <tr key={r.roomTypeId}>
                <td className="td">{types.find((t) => t.roomTypeId === r.roomTypeId)?.name ?? r.roomTypeId}</td>
                <td className="td">
                  <input type="number" min="0" className="input !py-1" value={r.contractedRate}
                    onChange={(e) => {
                      const rates = [...((edit?.rates ?? []) as RateRow[])];
                      rates[i] = { ...r, contractedRate: Number(e.target.value) };
                      setEdit({ ...edit!, rates });
                    }} />
                </td>
                <td className="td"><input type="checkbox" className="accent-zinc-900" checked={r.includesBreakfast}
                  onChange={(e) => {
                    const rates = [...((edit?.rates ?? []) as RateRow[])];
                    rates[i] = { ...r, includesBreakfast: e.target.checked };
                    setEdit({ ...edit!, rates });
                  }} /></td>
                <td className="td"><input type="checkbox" className="accent-zinc-900" checked={r.taxApplicable}
                  onChange={(e) => {
                    const rates = [...((edit?.rates ?? []) as RateRow[])];
                    rates[i] = { ...r, taxApplicable: e.target.checked };
                    setEdit({ ...edit!, rates });
                  }} /></td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setEdit(null)}>Cancel</button>
          <button className="btn-primary" onClick={save}
            disabled={!edit?.corporateCustomerId || !String(edit?.agreementNumber ?? "").trim()}>Save Agreement</button>
        </div>
      </Modal>
    </>
  );
}

