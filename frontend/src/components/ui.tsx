import type { ReactNode } from "react";
import { Card } from "@/components/ui/card";
import { Badge as ShadBadge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter
} from "@/components/ui/dialog";
import type { BadgeProps } from "@/components/ui/badge";

/* ------------------------------------------------------------------ layout */

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="page-title">{title}</h1>
        {subtitle && <p className="page-subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

/**
 * Semantic badge. `color` maps onto shadcn badge variants; kept as a prop for
 * backward compatibility with status-color tables across pages.
 */
const badgeColors = {
  green: "green", blue: "blue", amber: "amber",
  red: "red", violet: "violet", slate: "slate"
} as const;

export function Badge({ color = "slate", children }: { color?: keyof typeof badgeColors | string; children: ReactNode }) {
  const variant = (badgeColors as Record<string, BadgeProps["variant"]>)[color] ?? "secondary";
  return <ShadBadge variant={variant}>{children}</ShadBadge>;
}

export function StatCard({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <Card className="p-5">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      {hint && <div className="stat-hint">{hint}</div>}
    </Card>
  );
}

export function Spinner({ label = "Loading..." }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-16 text-muted-foreground">
      <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
      </svg>
      <span className="text-sm">{label}</span>
    </div>
  );
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-dashed py-14 text-center text-sm text-muted-foreground">
      {message}
    </div>
  );
}

/**
 * Accessible modal built on @radix-ui/react-dialog (focus-trap, Esc-to-close,
 * aria-labelledby). Preserves the original props contract used by pages.
 */
export function Modal({ open, title, onClose, children, wide }: {
  open: boolean; title: string; onClose: () => void; children: ReactNode; wide?: boolean;
}) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent wide={wide}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <div>{children}</div>
      </DialogContent>
    </Dialog>
  );
}

export function ConfirmDialog({ open, title, message, onConfirm, onCancel, danger }: {
  open: boolean; title: string; message: string;
  onConfirm: () => void; onCancel: () => void; danger?: boolean;
}) {
  return (
    <Modal open={open} title={title} onClose={onCancel}>
      <p className="text-sm text-muted-foreground">{message}</p>
      <DialogFooter className="mt-6 gap-2">
        <Button variant="outline" onClick={onCancel}>Cancel</Button>
        <Button variant={danger ? "destructive" : "default"} onClick={onConfirm}>Confirm</Button>
      </DialogFooter>
    </Modal>
  );
}
