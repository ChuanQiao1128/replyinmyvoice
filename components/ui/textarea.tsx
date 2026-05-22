import type { TextareaHTMLAttributes } from "react";

export function Textarea({
  className = "",
  ...props
}: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      className={`w-full resize-y rounded-md border border-line bg-cream px-3 py-2 text-sm leading-6 text-ink outline-none transition placeholder:text-ink/35 focus:border-evergreen focus:ring-2 focus:ring-evergreen/15 ${className}`}
      {...props}
    />
  );
}
