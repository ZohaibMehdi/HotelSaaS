import React, { useEffect, useState } from "react";
import { api } from "../api/client";
import type { Paged } from "../types";
import { Spinner, EmptyState } from "@/components/ui";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

interface Column<T> {
  header: string;
  render: (row: T) => React.ReactNode;
  className?: string;
}

export function DataTable<T extends { [k: string]: unknown }>({ columns, url, refreshKey, pageSize = 20, emptyMessage }: {
  columns: Column<T>[];
  url: string;                       // e.g. "/rooms" - query params appended automatically
  refreshKey?: unknown;
  pageSize?: number;
  emptyMessage?: string;
}) {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [data, setData] = useState<Paged<T> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { setPage(1); setSearch(searchInput === "" ? "" : search); /* noop keep */ }, [refreshKey]); // eslint-disable-line

  useEffect(() => {
    setLoading(true);
    const sep = url.includes("?") ? "&" : "?";
    api
      .get(`${url}${sep}page=${page}&pageSize=${pageSize}${search ? `&search=${encodeURIComponent(search)}` : ""}`)
      .then((r) => setData((r.data as unknown as { data: Paged<T> }).data))
      .catch((e) => setError(e.apiMessage ?? "Failed to load"))
      .finally(() => setLoading(false));
  }, [url, page, search, pageSize, refreshKey]);

  return (
    <div>
      <div className="flex items-center justify-between mb-3 gap-2">
        <form
          onSubmit={(e) => { e.preventDefault(); setPage(1); setSearch(searchInput); }}
          className="relative w-full max-w-xs"
        >
          <Input
            className="pl-8"
            placeholder="Search..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <svg className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </form>
        <span className="text-xs text-slate-400">{data ? `${data.totalCount} record(s)` : ""}</span>
      </div>

      {error && <div className="text-sm text-red-600 py-4">{error}</div>}
      {!error && loading && !data && <Spinner />}
      {data && data.items.length === 0 && <EmptyState message={emptyMessage ?? "No records found."} />}

      {data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto card">
            <table className="min-w-full divide-y divide-slate-100">
              <thead>
                <tr>{columns.map((c, i) => <th key={i} className={`th ${c.className ?? ""}`}>{c.header}</th>)}</tr>
              </thead>
              <tbody>
                {loading && <tr><td colSpan={columns.length}><Spinner /></td></tr>}
                {!loading && data.items.map((row, i) => (
                  <tr key={(row.id as number) ?? i} className="hover:bg-slate-50">
                    {columns.map((c, j) => (
                      <td key={j} className={`td ${c.className ?? ""}`}>{c.render(row)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pagination page={page} totalPages={Math.max(1, data.totalPages)} onChange={setPage} />
        </>
      )}
    </div>
  );
}

export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (p: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-end gap-1.5 mt-3">
      <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onChange(page - 1)}>Prev</Button>
      <span className="text-xs text-muted-foreground px-2">Page {page} of {totalPages}</span>
      <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Next</Button>
    </div>
  );
}



