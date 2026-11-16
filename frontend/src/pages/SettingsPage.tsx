import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { PropertySettings } from "../types";
import { PageHeader } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError, useAuth } from "../context/AuthContext";

export function CompanyInfoPage() {
  const [settings, setSettings] = useState<PropertySettings | null>(null);
  const [form, setForm] = useState<Record<string, unknown>>({});
  const [busy, setBusy] = useState(false);
  const { user } = useAuth();
  const canEdit = user?.roles.some((r) => ["TenantAdmin", "Manager"].includes(r));
  const { toast } = useToast();
  const apiError = useApiError();

  useEffect(() => {
    api.get("/company-information").then((r) => setForm(r.data.data));
    api.get("/property-settings").then((r) => setSettings(r.data.data));
  }, []);

  const saveCompany = async () => {
    setBusy(true);
    try {
      await api.put("/company-information", form);
      toast("success", "Company information saved");
    } catch (e) { toast("error", apiError(e)); } finally { setBusy(false); }
  };

  const saveSettings = async () => {
    try {
      await api.put("/property-settings", settings);
      toast("success", "Property settings saved");
    } catch (e) { toast("error", apiError(e)); }
  };

  if (!settings) return null;

  const companyFields: [string, string][] = [
    ["businessName", "Business Name *"], ["displayName", "Display Name"],
    ["legalName", "Legal Name"], ["registrationNumber", "Registration No"],
    ["taxNumber", "Tax Number"], ["postalCode", "Postal Code"],
    ["phone", "Phone"], ["email", "Email"], ["website", "Website"]
  ];

  return (
    <>
      <PageHeader title="Settings" subtitle="Company identity and property configuration" />

      <div className="grid lg:grid-cols-2 gap-5">
        <div className="card p-5">
          <h3 className="font-semibold text-sm text-slate-700 mb-4">Company Information (invoice header/footer)</h3>
          <div className="grid grid-cols-2 gap-3">
            {companyFields.map(([n, l]) => (
              <div key={n}>
                <label className="label">{l}</label>
                <input className="input disabled:bg-slate-50 disabled:text-slate-400" disabled={!canEdit}
                  value={(form[n] as string) ?? ""} onChange={(e) => setForm({ ...form, [n]: e.target.value })} />
              </div>
            ))}
            <div>
              <label className="label">Tax Percentage</label>
              <input type="number" min={0} max={100} step="0.01" className="input" disabled={!canEdit}
                value={(form.taxPercentage as number) ?? 0}
                onChange={(e) => setForm({ ...form, taxPercentage: Number(e.target.value) })} />
            </div>
            <div className="flex items-end pb-1">
              <label className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" className="accent-zinc-900" checked={!!form.taxEnabled} disabled={!canEdit}
                  onChange={(e) => setForm({ ...form, taxEnabled: e.target.checked })} /> Tax enabled
              </label>
            </div>
            <div className="col-span-2"><label className="label">Address</label>
              <textarea rows={2} className="input" disabled={!canEdit} value={(form.address as string) ?? ""}
                onChange={(e) => setForm({ ...form, address: e.target.value })} /></div>
            <div className="col-span-2"><label className="label">Invoice Header</label>
              <input className="input" disabled={!canEdit} value={(form.invoiceHeader as string) ?? ""}
                onChange={(e) => setForm({ ...form, invoiceHeader: e.target.value })} /></div>
            <div className="col-span-2"><label className="label">Invoice Footer</label>
              <input className="input" disabled={!canEdit} value={(form.invoiceFooter as string) ?? ""}
                onChange={(e) => setForm({ ...form, invoiceFooter: e.target.value })} /></div>
          </div>
          {canEdit && <button className="btn-primary mt-4" onClick={saveCompany} disabled={busy}>Save Company Info</button>}
        </div>

        <div className="card p-5">
          <h3 className="font-semibold text-sm text-slate-700 mb-4">Property Settings</h3>
          <div className="grid grid-cols-2 gap-3">
            <div><label className="label">Property Name</label>
              <input className="input" disabled={!canEdit} value={settings.propertyName}
                onChange={(e) => setSettings({ ...settings, propertyName: e.target.value })} /></div>
            <div><label className="label">Property Type</label>
              <select className="input" disabled={!canEdit} value={settings.propertyType}
                onChange={(e) => setSettings({ ...settings, propertyType: e.target.value })}>
                {["Hotel", "GuestHouse", "Hostel", "ServicedResidence", "Other"].map((t) => <option key={t}>{t}</option>)}
              </select></div>
            <div><label className="label">Check-in Time</label>
              <input type="time" className="input" disabled={!canEdit} value={settings.checkInTime.slice(0, 5)}
                onChange={(e) => setSettings({ ...settings, checkInTime: e.target.value })} /></div>
            <div><label className="label">Check-out Time</label>
              <input type="time" className="input" disabled={!canEdit} value={settings.checkOutTime.slice(0, 5)}
                onChange={(e) => setSettings({ ...settings, checkOutTime: e.target.value })} /></div>
            <div><label className="label">Tax Rate %</label>
              <input type="number" min={0} max={100} step="0.01" className="input" disabled={!canEdit}
                value={settings.taxRatePercent}
                onChange={(e) => setSettings({ ...settings, taxRatePercent: Number(e.target.value) })} /></div>
            <div><label className="label">Currency</label>
              <input className="input" disabled={!canEdit} maxLength={3} value={settings.currencyCode}
                onChange={(e) => setSettings({ ...settings, currencyCode: e.target.value.toUpperCase() })} /></div>
            <div><label className="label">Booking Prefix</label>
              <input className="input" disabled={!canEdit} value={settings.bookingPrefix}
                onChange={(e) => setSettings({ ...settings, bookingPrefix: e.target.value })} /></div>
            <div><label className="label">Invoice Prefix</label>
              <input className="input" disabled={!canEdit} value={settings.invoicePrefix}
                onChange={(e) => setSettings({ ...settings, invoicePrefix: e.target.value })} /></div>
            <div className="col-span-2 flex items-center gap-5 pt-1">
              <label className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" className="accent-zinc-900" disabled={!canEdit}
                  checked={settings.taxInclusive} onChange={(e) => setSettings({ ...settings, taxInclusive: e.target.checked })} />
                Rates are tax-inclusive
              </label>
              <label className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" className="accent-zinc-900" disabled={!canEdit}
                  checked={settings.defaultBreakfastIncluded} onChange={(e) => setSettings({ ...settings, defaultBreakfastIncluded: e.target.checked })} />
                Breakfast included by default
              </label>
            </div>
            <div className="col-span-2"><label className="label">Booking Policy</label>
              <textarea rows={2} className="input" disabled={!canEdit} value={settings.bookingPolicyText ?? ""}
                onChange={(e) => setSettings({ ...settings, bookingPolicyText: e.target.value })} /></div>
            <div className="col-span-2"><label className="label">Cancellation Policy</label>
              <textarea rows={2} className="input" disabled={!canEdit} value={settings.cancellationPolicyText ?? ""}
                onChange={(e) => setSettings({ ...settings, cancellationPolicyText: e.target.value })} /></div>
            <div className="col-span-2"><label className="label">Payment Policy</label>
              <textarea rows={2} className="input" disabled={!canEdit} value={settings.paymentPolicyText ?? ""}
                onChange={(e) => setSettings({ ...settings, paymentPolicyText: e.target.value })} /></div>
          </div>
          {canEdit && <button className="btn-primary mt-4" onClick={saveSettings}>Save Property Settings</button>}
        </div>
      </div>
    </>
  );
}

