
import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { Guest } from "../types";
import { labels } from "../types";
import { PageHeader, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";
import { fmtDate } from "@/lib/format";

const emptyForm = { idType: 0 };

export function GuestsPage() {
  const [guests, setGuests] = useState<Guest[]>([]);
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const [edit, setEdit] = useState<Record<string, unknown> | null>(null);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "100" });
    if (q) qs.set("search", q);
    api.get(`/guests?${qs}`).then((r) => setGuests(r.data.data.items));
  };
  useEffect(() => { load(); }, [q]); // eslint-disable-line

  const save = async () => {
    try {
      if (edit!.guestId) await api.put(`/guests/${edit!.guestId}`, edit);
      else await api.post("/guests", edit);
      toast("success", "Guest saved");
      setEdit(null); load();
    } catch (e) { toast("error", apiError(e)); }
  };
  const deactivate = async (g: Guest) => {
    try { await api.delete(`/guests/${g.guestId}`); load(); } catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Guests" subtitle="Central guest directory"
        actions={<button className="btn-primary" onClick={() => setEdit({ ...emptyForm })}>+ New Guest</button>} />

      <form onSubmit={(e) => { e.preventDefault(); setQ(search); }} className="mb-3 max-w-xs">
        <input className="input" placeholder="Search name / phone / ID..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </form>

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Name</th><th className="th">Phone</th><th className="th">ID Type</th><th className="th">ID Number</th><th className="th">City</th><th className="th">Created</th><th className="th">Actions</th></tr></thead>
          <tbody>
            {guests.map((g) => (
              <tr key={g.guestId} className={`hover:bg-slate-50 ${!g.isActive ? "opacity-50" : ""}`}>
                <td className="td font-medium">{g.firstName} {g.lastName}</td>
                <td className="td text-slate-500">{g.phone ?? "-"}</td>
                <td className="td">{labels.idTypes[g.idType]}</td>
                <td className="td font-mono text-xs">{g.idNumber ?? "-"}</td>
                <td className="td">{g.city ?? "-"}</td>
                <td className="td text-xs text-slate-400">{fmtDate(g.createdAt)}</td>
                <td className="td whitespace-nowrap">
                  <button className="btn-xs-secondary" onClick={() => setEdit({ ...g })}>Edit</button>
                  {g.isActive && (
                    <button className="btn-xs-danger ml-1"
                      onClick={() => { if (confirm(`Deactivate guest ${g.firstName}?`)) deactivate(g); }}>Deactivate</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal open={!!edit} title={edit?.guestId ? "Edit Guest" : "New Guest"} onClose={() => setEdit(null)} wide>
        <div className="grid grid-cols-2 gap-4">
          {([
            ["firstName", "First Name *"], ["lastName", "Last Name"], ["phone", "Phone"],
            ["email", "Email"], ["idNumber", "ID Number"], ["nationality", "Nationality"],
            ["city", "City"], ["country", "Country"]
          ] as [string, string][]).map(([n, l]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input className="input" value={(edit?.[n] as string) ?? ""} onChange={(e) => setEdit({ ...edit!, [n]: e.target.value })} />
            </div>
          ))}
          <div>
            <label className="label">Gender</label>
            <select className="input" value={String(edit?.gender ?? "")} onChange={(e) => setEdit({ ...edit!, gender: e.target.value === "" ? null : Number(e.target.value) })}>
              <option value="">--</option>
              <option value="0">Male</option><option value="1">Female</option><option value="2">Other</option>
            </select>
          </div>
          <div>
            <label className="label">ID Type</label>
            <select className="input" value={String(edit?.idType ?? 0)} onChange={(e) => setEdit({ ...edit!, idType: Number(e.target.value) })}>
              {labels.idTypes.map((t, i) => <option key={i} value={i}>{t}</option>)}
            </select>
          </div>
          <div className="col-span-2">
            <label className="label">Address</label>
            <textarea className="input" rows={2} value={(edit?.address as string) ?? ""} onChange={(e) => setEdit({ ...edit!, address: e.target.value })} />
          </div>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setEdit(null)}>Cancel</button>
          <button className="btn-primary" onClick={save}>Save</button>
        </div>
      </Modal>
    </>
  );
}

