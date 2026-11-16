import axios from "axios";

export const api = axios.create({ baseURL: "/api" });

let accessToken: string | null = localStorage.getItem("hs_access");
let refreshToken: string | null = localStorage.getItem("hs_refresh");
let onUnauthorized: (() => void) | null = null;
let refreshing: Promise<boolean> | null = null;

export function setTokens(access: string | null, refresh: string | null) {
  accessToken = access;
  refreshToken = refresh;
  if (access && refresh) {
    localStorage.setItem("hs_access", access);
    localStorage.setItem("hs_refresh", refresh);
  } else {
    localStorage.removeItem("hs_access");
    localStorage.removeItem("hs_refresh");
  }
}

export function getAccessToken() { return accessToken; }

export function registerUnauthorizedHandler(handler: () => void) { onUnauthorized = handler; }

api.interceptors.request.use((cfg) => {
  if (accessToken) cfg.headers.Authorization = `Bearer ${accessToken}`;
  return cfg;
});

async function tryRefresh(): Promise<boolean> {
  if (!refreshToken) return false;
  refreshing ??= (async () => {
    try {
      const resp = await axios.post("/api/auth/refresh", { refreshToken });
      const d = resp.data.data as { accessToken: string; refreshToken: string };
      setTokens(d.accessToken, d.refreshToken);
      return true;
    } catch {
      setTokens(null, null);
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

api.interceptors.response.use(
  (resp) => resp,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retried && !original.url.includes("/auth/")) {
      original._retried = true;
      if (await tryRefresh()) return api(original);
      onUnauthorized?.();
    }
    // Normalize API error shape for callers
    const data = error.response?.data;
    error.apiMessage =
      data?.message ??
      (data?.errors
        ? Object.entries(data.errors).map(([k, v]) => `${k}: ${(v as string[]).join(", ")}`).join("; ")
        : null) ??
      error.message ??
      "Request failed";
    return Promise.reject(error);
  }
);

export function extractApiError(e: unknown): string {
  const err = e as { apiMessage?: string; response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
  const d = err.response?.data;
  if (d?.errors)
    return Object.entries(d.errors).map(([k, v]) => `${k}: ${v.join(", ")}`).join("\n");
  return err.apiMessage ?? d?.message ?? "An unexpected error occurred.";
}
