import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/**
 * Semantic status badges. Legacy color keys (green/blue/…) are kept as variants
 * so existing call sites remain valid.
 */
const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-semibold transition-colors whitespace-nowrap",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary text-primary-foreground",
        secondary: "border-zinc-200 bg-zinc-50 text-zinc-600",
        outline: "text-foreground",
        destructive: "border-red-200 bg-red-50 text-red-700",
        green: "border-emerald-200 bg-emerald-50 text-emerald-700",
        blue: "border-sky-200 bg-sky-50 text-sky-700",
        amber: "border-amber-200 bg-amber-50 text-amber-700",
        red: "border-red-200 bg-red-50 text-red-700",
        violet: "border-violet-200 bg-violet-50 text-violet-700",
        slate: "border-zinc-200 bg-zinc-50 text-zinc-600"
      }
    },
    defaultVariants: { variant: "secondary" }
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
