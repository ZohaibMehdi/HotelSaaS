
import React, { useEffect, useState } from "react";
import { api } from "../api/client";
import type { HkTask } from "../types";
import { labels, HkTaskStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { fmtDateTime } from "@/lib/format";

const colColor: Record<number, string> = {
  [HkTaskStatus.Pending]: "bg-amber-50 border-amber-300",
  [HkTaskStatus.InProgress]: "bg-blue-50 border-blue-300",
  [HkTaskStatus.Completed]: "bg-emerald-50 border-emerald-300",
  [HkTaskStatus.Cancelled]: "bg-slate-100 border-slate-200"
};

export function HousekeepingPage() {
  const [tasks, setTasks] = useState<HkTask[]>([]);
  const [statusFilter, setStatusFilter] = useState<string>("");

  const load = () => {
    const qs = new URLSearchParams();
    if (statusFilter !== "") qs.set("status", statusFilter);
    api.get(`/housekeeping/tasks?${qs}`).then((r) => setTasks(r.data.data));
  };
  useEffect(() => { load(); }, [statusFilter]);

  const update = async (t: HkTask, s: HkTaskStatus) => {
    try { await api.patch(`/housekeeping/tasks/${t.taskId}/status/${s}`); load(); }
    catch (e) { (window as unknown as { lastErr?: string }).lastErr = String(e); load(); }
  };

  const columns: { key: number; title: string; items: HkTask[]; next?: HkTaskStatus; nextLabel?: string; cancelable?: boolean }[] = [
    { key: 0, title: "Pending", items: tasks.filter((t) => t.status === HkTaskStatus.Pending), next: HkTaskStatus.InProgress, nextLabel: "Start", cancelable: true },
    { key: 1, title: "In Progress", items: tasks.filter((t) => t.status === HkTaskStatus.InProgress), next: HkTaskStatus.Completed, nextLabel: "Complete" },
    { key: 2, title: "Completed", items: tasks.filter((t) => t.status === HkTaskStatus.Completed) },
    { key: 3, title: "Cancelled", items: tasks.filter((t) => t.status === HkTaskStatus.Cancelled) }
  ];

  return (
    <>
      <PageHeader title="Housekeeping" subtitle="Cleaning and inspection task board"
        actions={
          <select className="input w-40" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">All statuses</option>
            {labels.hkTaskStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
          </select>
        } />

      <div className="grid md:grid-cols-2 xl:grid-cols-4 gap-4">
        {columns.map((col) => (
          <div key={col.key}>
            <h3 className="text-xs font-bold uppercase tracking-wide text-slate-500 mb-2 px-1">
              {col.title} <span className="text-slate-400">({col.items.length})</span>
            </h3>
            <div className={`rounded-lg border p-2 space-y-2 min-h-24 ${colColor[col.key]}`}>
              {col.items.map((t) => (
                <div key={t.taskId} className="bg-white rounded-md p-3 shadow-sm text-sm">
                  <div className="flex justify-between items-center">
                    <span className="font-bold">Room {t.roomNumber}</span>
                    <Badge color={t.taskType === 1 ? "blue" : t.taskType === 2 ? "red" : "amber"}>{labels.hkTaskType[t.taskType]}</Badge>
                  </div>
                  {t.bookingNumber && <div className="text-[11px] text-slate-400 mt-0.5">After stay {t.bookingNumber}</div>}
                  <div className="text-[11px] text-slate-500">{fmtDateTime(t.scheduledFor)}</div>
                  {t.assignedToName && <div className="text-[11px] text-slate-400">Assigned: {t.assignedToName}</div>}
                  {(col.next !== undefined || col.cancelable) && (
                    <div className="flex gap-1.5 mt-2">
                      {col.next !== undefined && (
                        <button className={`!py-1 !px-2 !text-[11px] ${col.next === HkTaskStatus.Completed ? "btn-success" : "btn-primary"}`}
                          onClick={() => update(t, col.next!)}>
                          {col.nextLabel}
                        </button>
                      )}
                      {col.cancelable && (
                        <button className="btn-xs-secondary" onClick={() => update(t, HkTaskStatus.Cancelled)}>Cancel</button>
                      )}
                    </div>
                  )}
                  {t.completedAt && <div className="text-[10px] text-emerald-600 mt-1">Done {fmtDateTime(t.completedAt)}</div>}
                </div>
              ))}
              {col.items.length === 0 && <div className="text-center text-[11px] text-slate-400 py-6">Empty</div>}
            </div>
          </div>
        ))}
      </div>

      <p className="text-xs text-slate-400 mt-5">
        Completing a cleaning/inspection task automatically moves the room back to <b>Available</b> (PRD §31 lifecycle).
      </p>
      <NewTaskHint onCreated={load} />
    </>
  );
}

function NewTaskHint({ onCreated }: { onCreated: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button className="mt-3 btn-secondary" onClick={() => setOpen(true)}>+ Create Task Manually</button>
      <Modal open={open} title="Create Housekeeping Task" onClose={() => setOpen(false)}>
        <ManualTaskForm onDone={() => { setOpen(false); onCreated(); }} onCancel={() => setOpen(false)} />
      </Modal>
    </>
  );
}

function ManualTaskForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [form, setForm] = useState<Record<string, unknown>>({ taskType: 0 });
  const [rooms, setRooms] = useState<{ roomId: number; roomNumber: string }[]>([]);
  React.useEffect(() => {
    api.get("/rooms?page=1&pageSize=200").then((r) => setRooms(r.data.data.items));
  }, []);
  const create = async () => {
    try {
      await api.post("/housekeeping/tasks", { ...form, scheduledFor: new Date().toISOString() });
      onDone();
    } catch (e) { alert(String(e)); }
  };
  return (
    <div className="grid grid-cols-2 gap-4">
      <div className="col-span-2">
        <label className="label">Room *</label>
        <select className="input" onChange={(e) => setForm({ ...form, roomId: Number(e.target.value) })}>
          <option value="">--</option>
          {rooms.map((r) => <option key={r.roomId} value={r.roomId}>{r.roomNumber}</option>)}
        </select>
      </div>
      <div>
        <label className="label">Type</label>
        <select className="input" onChange={(e) => setForm({ ...form, taskType: Number(e.target.value) })}>
          {labels.hkTaskType.map((t, i) => <option key={i} value={i}>{t}</option>)}
        </select>
      </div>
      <div>
        <label className="label">Notes</label>
        <input className="input" onChange={(e) => setForm({ ...form, notes: e.target.value })} />
      </div>
      <div className="col-span-2 flex justify-end gap-2">
        <button className="btn-secondary" onClick={onCancel}>Close</button>
        <button className="btn-primary" onClick={create}>Create Task</button>
      </div>
    </div>
  );
}


