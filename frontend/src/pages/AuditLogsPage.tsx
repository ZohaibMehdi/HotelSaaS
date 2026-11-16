
import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { AuditLog } from "../types";
import { PageHeader, Badge } from "../components/ui";
import { fmtDateTime } from "@/lib/format";

const actionColor: Record<string, string> = {
  Create: "green", Update: "blue", Delete: "red", Deactivate: "red",
  CheckIn: "amber", CheckOut: "violet", Cancel: "red", Payment: "green", Finalize: "violet"
};

export function AuditLogsPage() {
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [entityFilter, setEntityFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const load = () => {
    const qs = new URLSearchParams({ page: String(page), pageSize: "50" });
    if (entityFilter) qs.set("entityName", entityFilter);
    api.get(`/audit-logs?${qs}`).then((r) => {
      setLogs(r.data.data.items);
      setTotalPages(Math.max(1, r.data.data.totalPages));
    });
  };
  useEffect(() => { load(); }, [page, entityFilter]); // eslint-disable-line

  return (
    <>
      <PageHeader title="Audit Logs" subtitle="Immutable trail of critical actions (append-only)"
        actions={
          <select className="input w-44" value={entityFilter} onChange={(e) => { setPage(1); setEntityFilter(e.target.value); }}>
            <option value="">All entities</option>
            {["Tenant", "User", "CompanyInformation", "PropertySettings", "RoomType", "Room", "Guest", "Booking", "Invoice", "CorporateCustomer", "B2BAgreement", "Employee", "PayrollRun", "HousekeepingTask"].map((e) =>
              <option key={e} value={e}>{e}</option>)}
          </select>
        } />

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Time</th><th className="th">User</th><th className="th">Action</th><th className="th">Entity</th><th className="th">Entity ID</th><th className="th">Details</th></tr></thead>
          <tbody>
            {logs.map((l) => (
              <tr key={l.auditLogId} className="hover:bg-slate-50 align-top">
                <td className="td text-xs whitespace-nowrap text-slate-500">{fmtDateTime(l.createdAt)}</td>
                <td className="td text-xs">{l.userName ?? "-"}</td>
                <td className="td"><Badge color={actionColor[l.action] ?? "slate"}>{l.action}</Badge></td>
                <td className="td text-xs">{l.entityName}</td>
                <td className="td font-mono text-[11px]">{l.entityId}</td>
                <td className="td font-mono text-[10px] text-slate-400 max-w-md truncate" title={`${l.oldValues ?? ""}${l.newValues ? `\n=> ${l.newValues}` : ""}`}>
                  {l.newValues ?? l.oldValues ?? "-"}
                </td>
              </tr>
            ))}
            {logs.length === 0 && <tr><td colSpan={6} className="td text-center text-xs text-slate-400 py-8">No audit entries.</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="flex justify-end gap-2 mt-3">
        <button className="btn-secondary !h-8 !px-3 !text-xs" disabled={page <= 1} onClick={() => setPage(page - 1)}>Prev</button>
        <span className="text-xs text-slate-500 self-center">Page {page} / {totalPages}</span>
        <button className="btn-secondary !h-8 !px-3 !text-xs" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</button>
      </div>
    </>
  );
}

