import Link from "next/link";
import type { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from "react";

const variants = {
  primary: "bg-sage text-white hover:bg-sage/90",
  secondary: "border border-line bg-white text-ink hover:bg-paper",
  ghost: "text-ink hover:bg-paper-deep",
  clay: "bg-clay text-white hover:bg-clay/90",
};

const controlBase =
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: keyof typeof variants;
};

export function Button({
  className = "",
  variant = "primary",
  ...props
}: ButtonProps) {
  return (
    <button
      className={`${controlBase} disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`}
      {...props}
    />
  );
}

type LinkButtonProps = AnchorHTMLAttributes<HTMLAnchorElement> & {
  href: string;
  children: ReactNode;
  variant?: keyof typeof variants;
};

export function LinkButton({
  className = "",
  href,
  variant = "primary",
  ...props
}: LinkButtonProps) {
  return (
    <Link
      className={`${controlBase} ${variants[variant]} ${className}`}
      href={href}
      {...props}
    />
  );
}
