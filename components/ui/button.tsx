import Link from "next/link";
import type { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from "react";

const variants = {
  primary: "bg-evergreen text-cream shadow-sm hover:bg-evergreen/90",
  secondary:
    "border border-line bg-cream text-ink hover:border-evergreen/35 hover:bg-mist/45",
  ghost: "text-ink hover:bg-mist/45",
  clay: "bg-brick text-white shadow-sm hover:bg-brick/90",
};

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
      className={`inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-evergreen disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`}
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
      className={`inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-evergreen ${variants[variant]} ${className}`}
      href={href}
      {...props}
    />
  );
}
