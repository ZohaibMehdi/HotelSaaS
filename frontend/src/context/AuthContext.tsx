import React, { createContext, useCallback, useContext, useEffect, useState } from "react";
import { api, extractApiError, registerUnauthorizedHandler, setTokens } from "../api/client";
import type { LoginResponse, UserSummary } from "../types";

interface AuthState {
  user: UserSummary | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState>(null!);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const access = localStorage.getItem("hs_access");
    if (!access) { setLoading(false); return; }
    api.get<never>("/auth/me")
      .then((r) => setUser((r.data as unknown as { data: UserSummary }).data))
      .catch(() => setTokens(null, null))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    registerUnauthorizedHandler(() => setUser(null));
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const resp = await api.post("/auth/login", { email, password });
    const d = (resp.data as { data: LoginResponse }).data;
    setTokens(d.accessToken, d.refreshToken);
    setUser(d.user);
  }, []);

  const logout = useCallback(() => {
    const refresh = localStorage.getItem("hs_refresh");
    api.post("/auth/logout", { refreshToken: refresh }).catch(() => undefined);
    setTokens(null, null);
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>{children}</AuthContext.Provider>
  );
}

export function useAuth() { return useContext(AuthContext); }

export function useApiError() {
  return useCallback((e: unknown) => extractApiError(e), []);
}
