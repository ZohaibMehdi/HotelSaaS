import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { UserSummary } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError, useAuth } from "../context/AuthContext";

const roleOptions = ["TenantAdmin", "Manager", "FrontDesk", "Finance", "HR", "Housekeeping"].map((r) => ({ value: r, label: r }));
const roleColor: Record<string, string> = { SuperAdmin: "violet", TenantAdmin: "blue", Manager: "amber", FrontDesk: "green", Finance: "slate", HR: "red", Housekeeping: "slate" };

export function UsersPage() {
  const { user: me } = useAuth();
  const isSuper = me?.roles.includes("SuperAdmin");
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [resetUser, setResetUser] = useState<UserSummary | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [form, setForm] = useState<Record<string, unknown>>({ role: "FrontDesk" });
  const [busy, setBusy] = useState(false);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "100" });
    if (query) qs.set("search", query);
    if (isSuper) qs.set("tenantId", ""); // all scopes
    api.get(`/users?${qs.toString()}`).then((r) => setUsers(r.data.data.items));
  };
  useEffect(() => { load(); }, []); // eslint-disable-line

  const submitCreate = async () => {
    setBusy(true);
    try {
      await api.post("/users", form);
      toast("success", "User created with temporary password");
      setCreateOpen(false); setForm({ role: "FrontDesk" }); load();
    } catch (e) { toast("error", apiError(e)); } finally { setBusy(false); }
  };

  const resetPassword = async () => {
    if (!resetUser) return;
    try {
      await api.post(`/users/${resetUser.userId}/reset-password`, { newPassword });
      toast("success", `Temporary password assigned to ${resetUser.email}`);
      setResetUser(null); setNewPassword("");
    } catch (e) { toast("error", apiError(e)); }
  };

  const toggleActive = async (u: UserSummary) => {
    try { await api.post(`/users/${u.userId}/active/${!u.isActive}`); load(); }
    catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Users" subtitle="Manage staff accounts and roles"
        actions={<button className="btn-primary" onClick={() => setCreateOpen(true)}>+ New User</button>} />

      <form onSubmit={(e) => { e.preventDefault(); setQuery(search); }} className="mb-3 max-w-xs">
        <input className="input" placeholder="Search name or email..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </form>

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Name</th><th className="th">Email</th><th className="th">{isSuper ? "Tenant" : "Phone"}</th><th className="th">Role</th><th className="th">Status</th><th className="th">Last Login</th><th className="th">Actions</th></tr></thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.userId} className="hover:bg-slate-50">
                <td className="td font-medium">{u.fullName}</td>
                <td className="td text-slate-500">{u.email}</td>
                <td className="td text-slate-500 text-xs">{isSuper ? u.tenantName ?? "Platform" : u.phone ?? "-"}</td>
                <td className="td">{u.roles.map((r) => <Badge key={r} color={roleColor[r] ?? "slate"}>{r}</Badge>)}</td>
                <td className="td"><Badge color={u.isActive ? "green" : "red"}>{u.isActive ? "Active" : "Disabled"}</Badge></td>
                <td className="td text-xs text-slate-400">{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "-"}</td>
                <td className="td whitespace-nowrap">
                  <button className="btn-xs-secondary" onClick={() => setResetUser(u)}>Reset Password</button>
                  {u.userId !== me?.userId && (
                    <button className={`ml-1 !py-1 !px-2 !text-xs ${u.isActive ? "btn-danger" : "btn-success"}`} onClick={() => toggleActive(u)}>
                      {u.isActive ? "Disable" : "Enable"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal open={createOpen} title="Create User" onClose={() => setCreateOpen(false)}>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {[["fullName", "Full Name"], ["email", "Email"], ["phone", "Phone"], ["password", "Initial Password"]].map(([n, l]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input className="input" value={(form[n] as string) ?? ""}
                onChange={(e) => setForm({ ...form, [n]: e.target.value })} />
            </div>
          ))}
          <div>
            <label className="label">Role</label>
            <select className="input" value={(form.role as string) ?? ""} onChange={(e) => setForm({ ...form, role: e.target.value })}>
              {(isSuper ? [{ value: "SuperAdmin", label: "SuperAdmin" }, ...roleOptions] : roleOptions).map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setCreateOpen(false)}>Cancel</button>
          <button className="btn-primary" onClick={submitCreate} disabled={busy}>Create</button>
        </div>
      </Modal>

      <Modal open={!!resetUser} title={`Reset password - ${resetUser?.email ?? ""}`} onClose={() => setResetUser(null)}>
        <label className="label">New temporary password</label>
        <input className="input" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
        <p className="text-xs text-slate-400 mt-1.5">The user must change this password at next login.</p>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setResetUser(null)}>Cancel</button>
          <button className="btn-primary" onClick={resetPassword}>Assign</button>
        </div>
      </Modal>
    </>
  );
}




