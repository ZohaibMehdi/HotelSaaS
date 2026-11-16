import React, { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth, useApiError } from "../context/AuthContext";
import { Button } from "@/components/ui/button";

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation() as { state?: { from?: { pathname: string } } };
  const apiError = useApiError();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
      navigate(location.state?.from?.pathname ?? "/dashboard", { replace: true });
    } catch (err) {
      setError(apiError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card w-full max-w-md p-8">
      <div className="text-center mb-7">
        <div className="text-2xl font-bold text-primary">HotelSaaS</div>
        <p className="text-sm text-slate-500 mt-1">Hotel &amp; Guest House Management</p>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 text-red-600 text-sm px-3 py-2 whitespace-pre-line">{error}</div>}

      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label">Email</label>
          <input className="input" type="email" required autoFocus value={email}
            onChange={(e) => setEmail(e.target.value)} placeholder="you@yourhotel.com" />
        </div>
        <div>
          <label className="label">Password</label>
          <input className="input" type="password" required value={password}
            onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" />
        </div>
        <Button type="submit" className="mt-2 w-full" disabled={busy}>
            {busy ? "Signing in..." : "Sign in"}
          </Button>
      </form>

      <div className="mt-6 rounded-md bg-slate-50 border border-slate-200 p-3 text-[11px] text-slate-500 leading-relaxed">
        <span className="font-semibold text-slate-600">Demo accounts (password: Admin@123)</span><br />
        Platform: superadmin@hotelsaas.com<br />
        Saibaan GH: admin@saibaan.com · frontdesk@saibaan.com · finance@saibaan.com · hr@saibaan.com · housekeeping@saibaan.com<br />
        Hotel ABC: admin@hotelabc.com
      </div>
    </div>
  );
}


