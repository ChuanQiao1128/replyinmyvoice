import type { InputHTMLAttributes } from "react";

export function Input({ className = "", ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`w-full rounded-md border border-line bg-cream px-3 py-2 text-sm text-ink outline-none transition placeholder:text-ink/35 focus:border-evergreen focus:ring-2 focus:ring-evergreen/15 ${className}`}
      {...props}
    />
  );
}
