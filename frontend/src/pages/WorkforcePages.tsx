
import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { Employee, PayrollRun, DepartmentDto } from "../types";
import { labels, EmploymentStatus } from "../types";
import { PageHeader, Badge, Modal } from "../components/ui";
import { useToast } from "../context/ToastContext";
import { useApiError } from "../context/AuthContext";
import { fmtMoney, fmtDate } from "@/lib/format";

const empColor: Record<number, string> = { 0: "amber", 1: "green", 2: "blue", 3: "red", 4: "slate" };

export function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [deptFilter, setDeptFilter] = useState("");
  const [search, setSearch] = useState("");
  const [edit, setEdit] = useState<Record<string, unknown> | null>(null);
  const [newDept, setNewDept] = useState("");
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () => {
    const qs = new URLSearchParams({ page: "1", pageSize: "200" });
    if (search) qs.set("search", search);
    if (deptFilter) qs.set("departmentId", deptFilter);
    return Promise.all([
      api.get(`/employees?${qs}`).then((r) => setEmployees(r.data.data.items)),
      api.get("/employees/departments").then((r) => setDepartments(r.data.data))
    ]);
  };
  useEffect(() => { load(); }, [search, deptFilter]);

  const save = async () => {
    try {
      if (edit!.employeeId) await api.put(`/employees/${edit!.employeeId}`, edit);
      else await api.post("/employees", edit);
      toast("success", "Employee saved");
      setEdit(null); load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const addDepartment = async () => {
    try { await api.post("/employees/departments", { name: newDept }); setNewDept(""); load(); }
    catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Employees" subtitle="Workforce directory and salaries"
        actions={<button className="btn-primary" onClick={() => setEdit({
          joiningDate: new Date().toISOString().slice(0, 10), employmentStatus: EmploymentStatus.Active
        })}>+ New Employee</button>} />

      <div className="flex gap-2 mb-3">
        <input className="input max-w-xs" placeholder="Search name / code..." value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="input w-44" value={deptFilter} onChange={(e) => setDeptFilter(e.target.value)}>
          <option value="">All departments</option>
          {departments.map((d) => <option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}
          <option value="__new">+ New department...</option>
        </select>
        {deptFilter === "__new" && (
          <span className="flex gap-1.5">
            <input className="input" placeholder="Dept name" value={newDept} onChange={(e) => setNewDept(e.target.value)} />
            <button className="btn-primary !h-8 !px-3 !text-xs" onClick={addDepartment}>Add</button>
          </span>
        )}
      </div>

      <div className="card overflow-x-auto">
        <table className="min-w-full">
          <thead><tr><th className="th">Code</th><th className="th">Name</th><th className="th">Department</th><th className="th">Designation</th><th className="th">Joined</th><th className="th text-right">Salary</th><th className="th">Status</th><th className="th">Actions</th></tr></thead>
          <tbody>
            {employees.map((e) => (
              <tr key={e.employeeId} className={`hover:bg-slate-50 ${!e.isActive ? "opacity-50" : ""}`}>
                <td className="td font-mono text-xs">{e.employeeCode}</td>
                <td className="td font-medium">{e.firstName} {e.lastName}</td>
                <td className="td text-xs">{e.departmentName ?? "-"}</td>
                <td className="td text-xs">{e.designation ?? "-"}</td>
                <td className="td text-xs">{fmtDate(e.joiningDate)}</td>
                <td className="td text-right">{fmtMoney(e.basicSalary)}</td>
                <td className="td"><Badge color={empColor[e.employmentStatus]}>{labels.employmentStatus[e.employmentStatus]}</Badge></td>
                <td className="td"><button className="btn-xs-secondary" onClick={() => setEdit({ ...e })}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal open={!!edit} title={edit?.employeeId ? `Edit ${String(edit.employeeCode)}` : "New Employee"} onClose={() => setEdit(null)} wide>
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
          {([
            ["employeeCode", "Employee Code *"], ["firstName", "First Name *"], ["lastName", "Last Name"],
            ["cnic", "CNIC / ID"], ["phone", "Phone"], ["email", "Email"], ["designation", "Designation"],
            ["basicSalary", "Basic Salary *"], ["bankName", "Bank Name"], ["bankAccountNumber", "Bank Account"],
            ["emergencyContactName", "Emergency Contact"], ["emergencyContactPhone", "Emergency Phone"]
          ] as [string, string][]).map(([n, l]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input className="input" value={(edit?.[n] as string) ?? ""} onChange={(e) => setEdit({ ...edit!, [n]: e.target.value })} />
            </div>
          ))}
          <div>
            <label className="label">Joining Date *</label>
            <input type="date" className="input" value={(edit?.joiningDate as string)?.slice(0, 10) ?? ""}
              onChange={(e) => setEdit({ ...edit!, joiningDate: e.target.value })} />
          </div>
          <div>
            <label className="label">Department</label>
            <select className="input" value={(edit?.departmentId as number) ?? ""}
              onChange={(e) => setEdit({ ...edit!, departmentId: e.target.value === "" ? null : Number(e.target.value) })}>
              <option value="">--</option>
              {departments.map((d) => <option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}
            </select>
          </div>
          <div>
            <label className="label">Status</label>
            <select className="input" value={String(edit?.employmentStatus ?? 1)}
              onChange={(e) => setEdit({ ...edit!, employmentStatus: Number(e.target.value) })}>
              {labels.employmentStatus.map((s, i) => <option key={i} value={i}>{s}</option>)}
            </select>
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

const payColor: Record<number, string> = { 0: "amber", 1: "blue", 2: "green" };

export function PayrollPage() {
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [selected, setSelected] = useState<PayrollRun | null>(null);
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [itemEdit, setItemEdit] = useState<Record<string, unknown> | null>(null);
  const { toast } = useToast();
  const apiError = useApiError();

  const load = () =>
    api.get("/payroll").then(async (r) => {
      setRuns(r.data.data);
      if (selected) {
        const fresh = r.data.data.find((x: PayrollRun) => x.payrollRunId === selected.payrollRunId);
        if (fresh) setSelected(await api.get(`/payroll/${fresh.payrollRunId}`).then((rr) => rr.data.data));
      }
    });
  useEffect(() => { load(); }, []); // eslint-disable-line

  const createRun = async () => {
    try {
      const [y, m] = month.split("-").map(Number);
      const r = await api.post("/payroll", { year: y, month: m });
      toast("success", "Draft run created");
      setSelected(r.data.data); load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const action = async (runId: number, path: string, msg: string) => {
    try {
      const r = await api.post(`/payroll/${runId}/${path}`);
      toast("success", msg);
      setSelected(r.data.data); load();
    } catch (e) { toast("error", apiError(e)); }
  };

  const saveItem = async () => {
    try {
      await api.post(`/payroll/${selected!.payrollRunId}/items`, itemEdit);
      toast("success", "Payroll item saved");
      setItemEdit(null);
      const fresh = await api.get(`/payroll/${selected!.payrollRunId}`);
      setSelected(fresh.data.data);
    } catch (e) { toast("error", apiError(e)); }
  };

  return (
    <>
      <PageHeader title="Payroll" subtitle="Monthly payroll runs — finalized runs are immutable"
        actions={
          <span className="flex gap-2 items-center">
            <input type="month" className="input w-40" value={month} onChange={(e) => setMonth(e.target.value)} />
            <button className="btn-primary" onClick={createRun}>+ Create Run</button>
          </span>
        } />

      <div className="grid lg:grid-cols-3 gap-5">
        <div className="space-y-3">
          {runs.map((r) => (
            <button key={r.payrollRunId}
              onClick={async () => setSelected(await api.get(`/payroll/${r.payrollRunId}`).then((rr) => rr.data.data))}
              className={`w-full card p-4 text-left transition-shadow ${selected?.payrollRunId === r.payrollRunId ? "ring-2 ring-ring" : "hover:shadow-md"}`}>
              <div className="flex justify-between items-center">
                <span className="font-semibold">{r.payrollMonth.slice(0, 7)}</span>
                <Badge color={payColor[r.status]}>{labels.payrollStatus[r.status]}</Badge>
              </div>
              <div className="text-xs text-slate-500 mt-1">
                Net {fmtMoney(r.totalNet)} · Gross {fmtMoney(r.totalGross)}
              </div>
            </button>
          ))}
          {runs.length === 0 && <p className="text-xs text-slate-400 p-4 card">No payroll runs yet.</p>}
        </div>

        {selected && (
          <div className="lg:col-span-2 space-y-4">
            <div className="card p-4 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold">Payroll {selected.payrollMonth.slice(0, 7)}
                  <Badge color={payColor[selected.status]}>{labels.payrollStatus[selected.status]}</Badge>
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">{selected.periodStart} → {selected.periodEnd}</p>
              </div>
              <div className="text-right text-sm">
                <div>Gross: <b>{fmtMoney(selected.totalGross)}</b></div>
                <div>Deductions: <b>{fmtMoney(selected.totalDeductions)}</b></div>
                <div>Net payable: <b className="text-emerald-700">{fmtMoney(selected.totalNet)}</b></div>
              </div>
              <div className="flex gap-2">
                {selected.status === 0 && (
                  <>
                    <button className="btn-secondary" onClick={() => action(selected.payrollRunId, "generate", "Active employees added")}>Generate Items</button>
                    <button className="btn-primary" onClick={() => action(selected.payrollRunId, "finalize", "Payroll finalized — now immutable")}>Finalize</button>
                  </>
                )}
                {selected.status === 1 && (
                  <button className="btn-success" onClick={() => action(selected.payrollRunId, "mark-paid", "Marked as paid")}>Mark Paid</button>
                )}
              </div>
            </div>

            <div className="card overflow-x-auto">
              <table className="min-w-full">
                <thead><tr><th className="th">Emp</th><th className="th">Name</th><th className="th text-right">Basic</th><th className="th text-right">Allow.</th><th className="th text-right">OT</th><th className="th text-right">Bonus</th><th className="th text-right">Deductions</th><th className="th text-right">Gross</th><th className="th text-right">Net</th>{selected.status === 0 && <th className="th"></th>}</tr></thead>
                <tbody>
                  {(selected.items ?? []).map((it) => (
                    <tr key={it.payrollItemId} className="hover:bg-slate-50 cursor-pointer"
                      onClick={() => selected.status === 0 && setItemEdit({
                        employeeId: it.employeeId, allowances: it.allowances, overtimeAmount: it.overtimeAmount,
                        bonusAmount: it.bonusAmount, deductions: it.deductions,
                        advanceDeduction: it.advanceDeduction, loanDeduction: it.loanDeduction,
                        absenceDeduction: it.absenceDeduction
                      })}>
                      <td className="td font-mono text-xs">{it.employeeCode}</td>
                      <td className="td">{it.employeeName}<span className="block text-[11px] text-slate-400">{it.department}</span></td>
                      <td className="td text-right">{fmtMoney(it.basicSalary)}</td>
                      <td className="td text-right">{fmtMoney(it.allowances)}</td>
                      <td className="td text-right">{fmtMoney(it.overtimeAmount)}</td>
                      <td className="td text-right">{fmtMoney(it.bonusAmount)}</td>
                      <td className="td text-right text-red-600">
                        {fmtMoney(it.deductions + it.advanceDeduction + it.loanDeduction + it.absenceDeduction)}
                      </td>
                      <td className="td text-right font-medium">{fmtMoney(it.grossSalary)}</td>
                      <td className="td text-right font-bold">{fmtMoney(it.netSalary)}</td>
                      {selected.status === 0 && <td className="td text-xs text-primary">edit</td>}
                    </tr>
                  ))}
                  {(!selected.items || selected.items.length === 0) && (
                    <tr><td colSpan={10} className="td text-center text-xs text-slate-400 py-6">No items — click Generate Items.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      <Modal open={!!itemEdit} title={`Adjust item — ${String(itemEdit?.employeeId ?? "")}`} onClose={() => setItemEdit(null)}>
        <div className="grid grid-cols-2 gap-4">
          {[["allowances", "Allowances"], ["overtimeAmount", "Overtime"], ["bonusAmount", "Bonus"],
            ["deductions", "Deductions"], ["advanceDeduction", "Advance Recovery"], ["loanDeduction", "Loan Installment"],
            ["absenceDeduction", "Absence Deduction"]].map(([n, l]) => (
            <div key={n}>
              <label className="label">{l}</label>
              <input type="number" min="0" step="0.01" className="input" value={(itemEdit?.[n] as number) ?? 0}
                onChange={(e) => setItemEdit({ ...itemEdit!, [n]: Number(e.target.value) })} />
            </div>
          ))}
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => setItemEdit(null)}>Cancel</button>
          <button className="btn-primary" onClick={saveItem}>Save Item</button>
        </div>
      </Modal>
    </>
  );
}

