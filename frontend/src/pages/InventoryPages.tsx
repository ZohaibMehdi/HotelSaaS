
import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { RoomType, Room } from "../types";
import { labels, HkStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError, useAuth } from "../context/AuthContext";
import { fmtMoney } from "@/lib/format";

const statusColor: Record<number, string> = { 0: "green", 1: "blue", 2: "amber", 3: "violet", 4: "red", 5: "slate" };

export function RoomTypesPage() {
  const [types, setTypes] = useState<RoomType[]>([]);
  const [edit, setEdit] = useState<Partial<RoomType> | null>(null);
  const { toast } = useToast();
  const apiError = useApiError();
  const load = () => api.get("/room-types?includeInactive=true").then((r) => setTypes(r.data.data));
  useEffect(() => { load(); }, []);

  const save = async () => {
    try {
      if (edit!.roomTypeId) await api.put(`/room-types/${edit!.roomTypeId}`, edit);
      else await api.post("/room-types", edit);
      toast("success", "Room type saved");
      setEdit(null); load();
    } catch (e) { toast("error", apiError(e)); }
  };
  const deactivate = async (t: RoomType) => {
    try { await api.delete(`/room-types/${t.roomTypeId}`); load(); } catch (e) { toast("error", apiError(e)); }
  };

  const fields: [string, string, string?][] = [
    ["name", "Name"], ["basePrice", "Base Price / night", "number"],
    ["maxOccupancy", "Max Occupancy", "number"], ["extraAdultPrice", "Extra Adult Price", "number"],
    ["numberOfBeds", "Number of Beds", "number"], ["extraChildPrice", "Extra Child Price", "number"],
    ["bedConfiguration", "Bed Configuration"]
  ];

  return (
    <>
      <PageHeader title="Room Types" subtitle="Categories and pricing"
        actions={<button className="btn-primary" onClick={() => setEdit({ maxOccupancy: 2, numberOfBeds: 1 })}>+ New Room Type</button>} />
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
        {types.map((t) => (
          <div key={t.roomTypeId} className="card p-4">
            <div className="flex justify-between items-start">
              <h3 className="font-semibold text-slate-800">{t.name}</h3>
              <Badge color={t.isActive ? "green" : "slate"}>{t.isActive ? "Active" : "Inactive"}</Badge>
            </div>
            <p className="text-xs text-slate-400 mt-1 line-clamp-2">{t.description}</p>
            <div className="mt-3 flex items-baseline gap-1">
              <span className="text-xl font-bold text-primary">{fmtMoney(t.basePrice)}</span>
              <span className="text-xs text-slate-400">/night</span>
            </div>
            <div className="text-xs text-slate-500 mt-2">
              Max {t.maxOccupancy} guests · {t.numberOfBeds} bed(s) {t.bedConfiguration ? `· ${t.bedConfiguration}` : ""}
            </div>
            {t.amenities && <div className="text-[11px] text-slate-400 mt-1.5">{t.amenities}</div>}
            <div className="mt-3 flex gap-2">
              <button className="btn-xs-secondary" onClick={() => setEdit(t)}>Edit</button>
              {t.isActive && <button className="btn-xs-danger" onClick={() => deactivate(t)}>Deactivate</button>}
            </div>
          </div>
        ))}
      </div>

      <Modal open={!!edit} title={edit?.roomTypeId ? "Edit Room Type" : "New Room Type"} onClose={() => setEdit(null)}>
        <div className="grid grid-cols-2 gap-4">
          {fields.map(([n, l, ty]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input className="input" type={ty ?? "text"} value={(edit?.[n as keyof RoomType] as string) ?? ""}
                onChange={(e) => setEdit({ ...edit!, [n]: ty === "number" ? Number(e.target.value) : e.target.value })} />
            </div>
          ))}
          <div className="col-span-2">
            <label className="label">Description</label>
            <textarea className="input" rows={2} value={(edit?.description as string) ?? ""}
              onChange={(e) => setEdit({ ...edit!, description: e.target.value })} />
          </div>
          <div className="col-span-2">
            <label className="label">Amenities (comma separated)</label>
            <input className="input" value={(edit?.amenities as string) ?? ""}
              onChange={(e) => setEdit({ ...edit!, amenities: e.target.value })} placeholder="AC,TV,WiFi" />
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

export function RoomsPage() {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const [edit, setEdit] = useState<Record<string, unknown> | null>(null);
  const [types, setTypes] = useState<RoomType[]>([]);
  const { user } = useAuth();
  const { toast } = useToast();
  const apiError = useApiError();
  const canManage = user?.roles.some((r) => ["TenantAdmin", "Manager"].includes(r));
  const canHk = user?.roles.some((r) => ["TenantAdmin", "Manager", "Housekeeping"].includes(r));

  const loadRooms = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "200" });
    if (q) qs.set("search", q);
    if (statusFilter !== "") qs.set("status", statusFilter);
    return api.get(`/rooms?${qs}`).then((r) => setRooms(r.data.data.items));
  };
  useEffect(() => {
    api.get("/room-types").then((r) => setTypes(r.data.data));
  }, []);
  useEffect(() => { loadRooms(); }, [q, statusFilter]); // eslint-disable-line

  const save = async () => {
    try {
      if (edit!.roomId) await api.put(`/rooms/${edit!.roomId}`, edit);
      else await api.post("/rooms", edit);
      toast("success", "Room saved");
      setEdit(null); loadRooms();
    } catch (e) { toast("error", apiError(e)); }
  };

  const setHk = async (room: Room, hk: HkStatus) => {
    try { await api.patch(`/rooms/${room.roomId}/housekeeping`, { status: hk }); loadRooms(); }
    catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Rooms" subtitle="Physical room inventory"
        actions={canManage ? <button className="btn-primary" onClick={() => setEdit({ isActive: true })}>+ New Room</button> : undefined} />

      <div className="flex gap-2 mb-3">
        <form onSubmit={(e) => { e.preventDefault(); setQ(search); }}>
          <input className="input w-52" placeholder="Search room no..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </form>
        <select className="input w-44" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All statuses</option>
          {labels.roomStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
        </select>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
        {rooms.map((r) => (
          <div key={r.roomId} className={`card p-3.5 border-l-4 ${r.status === 0 ? "border-l-emerald-500" : r.status === 2 ? "border-l-amber-500" : r.status === 1 ? "border-l-blue-500" : r.status === 3 ? "border-l-violet-500" : "border-l-red-500"}`}>
            <div className="flex justify-between items-center">
              <span className="font-bold text-lg">{r.roomNumber}</span>
              <Badge color={statusColor[r.status]}>{labels.roomStatus[r.status]}</Badge>
            </div>
            <div className="text-xs text-slate-500 mt-0.5">{r.roomTypeName}{r.floor != null ? ` · Floor ${r.floor}` : ""}</div>
            {r.housekeepingStatus !== HkStatus.Clean && (
              <div className="text-[11px] text-amber-600 mt-0.5">HK: {labels.hkStatus[r.housekeepingStatus]}</div>
            )}
            <div className="mt-2.5 flex flex-wrap gap-1.5">
              {canHk && r.status === 3 && (
                <button className="btn-xs-secondary" onClick={() => setHk(r, HkStatus.Cleaning)}>Start Cleaning</button>
              )}
              {canHk && (r.status === 3 || r.housekeepingStatus === HkStatus.Dirty || r.housekeepingStatus === HkStatus.Cleaning) && (
                <button className="btn-xs-success" onClick={() => setHk(r, HkStatus.Clean)}>Mark Clean</button>
              )}
              {canManage && (
                <button className="btn-xs-secondary" onClick={() => setEdit({
                  roomId: r.roomId, roomTypeId: r.roomTypeId, roomNumber: r.roomNumber, floor: r.floor, notes: r.notes, isActive: r.isActive
                })}>Edit</button>
              )}
            </div>
          </div>
        ))}
      </div>

      <Modal open={!!edit} title={edit?.roomId ? `Edit Room ${String(edit.roomNumber)}` : "New Room"} onClose={() => setEdit(null)}>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="label">Room Number *</label>
            <input className="input" value={(edit?.roomNumber as string) ?? ""} onChange={(e) => setEdit({ ...edit!, roomNumber: e.target.value })} />
          </div>
          <div>
            <label className="label">Floor</label>
            <input className="input" type="number" value={(edit?.floor as number) ?? ""}
              onChange={(e) => setEdit({ ...edit!, floor: e.target.value === "" ? null : Number(e.target.value) })} />
          </div>
          <div className="col-span-2">
            <label className="label">Room Type *</label>
            <select className="input" value={String(edit?.roomTypeId ?? "")} onChange={(e) => setEdit({ ...edit!, roomTypeId: Number(e.target.value) })}>
              <option value="">-- select --</option>
              {types.filter((t) => t.isActive).map((t) => <option key={t.roomTypeId} value={t.roomTypeId}>{t.name}</option>)}
            </select>
          </div>
          <div className="col-span-2">
            <label className="label">Notes</label>
            <textarea className="input" rows={2} value={(edit?.notes as string) ?? ""} onChange={(e) => setEdit({ ...edit!, notes: e.target.value })} />
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

