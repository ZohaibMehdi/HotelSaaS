
import React, { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { Tenant } from "../types";
import { labels } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";
import { fmtDate } from "@/lib/format";

const statusColor: Record<number, string> = { 0: "amber", 1: "green", 2: "red", 3: "slate" };

export function TenantsPage() {
  const [tenants, setTenants] = useState<Tenant[] | null>(null);
  const [onboardOpen, setOnboardOpen] = useState(false);
  const [form, setForm] = useState<Record<string, unknown>>({ timeZone: "Pakistan Standard Time", currency: "PKR" });
  const [busy, setBusy] = useState(false);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => api.get("/tenants?page=1&pageSize=100").then((r) => setTenants((r.data.data.items) as Tenant[]));
  React.useEffect(() => { load(); }, []);

  const onboardFields = [
    { name: "tenantCode", label: "Tenant Code", required: true, placeholder: "e.g. MYHOTEL" },
    { name: "businessName", label: "Business Name", required: true },
    { name: "legalName", label: "Legal Name" },
    { name: "contactEmail", label: "Contact Email", required: true, type: "email" },
    { name: "contactPhone", label: "Contact Phone" },
    { name: "address", label: "Address" },
    { name: "city", label: "City" },
    { name: "country", label: "Country" },
    { name: "timeZone", label: "Time Zone", required: true },
    { name: "currency", label: "Currency", required: true },
    { name: "adminFullName", label: "Admin Full Name", required: true },
    { name: "adminEmail", label: "Admin Email", required: true, type: "email" },
    { name: "adminPhone", label: "Admin Phone" },
    { name: "initialPassword", label: "Initial Admin Password", required: true, type: "password" }
  ];

  const submitOnboard = async () => {
    setBusy(true);
    try {
      await api.post("/tenants/onboard", form);
      toast("success", "Tenant onboarded successfully");
      setOnboardOpen(false);
      setForm({ timeZone: "Pakistan Standard Time", currency: "PKR" });
      await load();
    } catch (e) {
      toast("error", apiError(e));
    } finally { setBusy(false); }
  };

  const setStatus = async (t: Tenant, status: number) => {
    try { await api.post(`/tenants/${t.tenantId}/status/${status}`); await load(); }
    catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Tenants" subtitle="Properties operating on the platform"
        actions={<button className="btn-primary" onClick={() => setOnboardOpen(true)}>+ Onboard Tenant</button>} />

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Code</th><th className="th">Business</th><th className="th">Status</th><th className="th">City</th><th className="th">Currency</th><th className="th">Onboarded</th><th className="th">Actions</th></tr></thead>
          <tbody>
            {(tenants ?? []).map((t) => (
              <tr key={t.tenantId} className="hover:bg-slate-50">
                <td className="td font-mono text-xs">{t.tenantCode}</td>
                <td className="td"><Link to={`/tenants/${t.tenantId}`} className="font-medium hover:text-primary">{t.businessName}</Link></td>
                <td className="td"><Badge color={statusColor[t.status]}>{labels.tenantStatus[t.status]}</Badge></td>
                <td className="td">{t.city ?? "-"}</td>
                <td className="td">{t.currency}</td>
                <td className="td text-xs text-slate-400">{fmtDate(t.createdAt)}</td>
                <td className="td">
                  {t.status === 1 ? (
                    <button className="btn-xs-secondary" onClick={() => setStatus(t, 3)}>Deactivate</button>
                  ) : (
                    <button className="btn-xs-success" onClick={() => setStatus(t, 1)}>Activate</button>
                  )}
                  {t.status !== 2 && t.status === 1 && (
                    <button className="btn-xs-secondary ml-1" onClick={() => setStatus(t, 2)}>Suspend</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal open={onboardOpen} title="Onboard New Tenant" onClose={() => setOnboardOpen(false)} wide>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {onboardFields.map((f) => (
            <div key={f.name}>
              <label className="label">{f.label} *</label>
              <input className="input" type={f.type ?? "text"} value={(form[f.name] as string) ?? ""}
                onChange={(e) => setForm({ ...form, [f.name]: e.target.value })} />
            </div>
          ))}
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setOnboardOpen(false)}>Cancel</button>
          <button className="btn-primary" onClick={submitOnboard} disabled={busy}>{busy ? "Creating..." : "Create Tenant + Admin"}</button>
        </div>
      </Modal>
    </>
  );
}

export function TenantDetailPage() {
  return <div className="card p-6 text-sm text-slate-500">Select a tenant from the list for details.</div>;
}

