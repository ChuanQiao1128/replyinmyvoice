import type { ShellIconName } from "./shell-types";

const PATHS: Record<ShellIconName, string> = {
  pen: "M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z",
  history: "M3 3v5h5 M3.05 13a9 9 0 1 0 2.6-6.4L3 8 M12 7v5l3 2",
  key: "M21 2l-2 2 M15 7a4 4 0 1 0-4 4l-7 7v3h3l1-1h2v-2h2l3.5-3.5A4 4 0 0 0 15 7Z",
  chart: "M3 3v18h18 M7 14v3 M12 9v8 M17 5v12",
  plug: "M9 2v6 M15 2v6 M7 8h10v3a5 5 0 0 1-10 0Z M12 16v6",
  card: "M2 6h20v12H2z M2 10h20",
  user: "M20 21a8 8 0 0 0-16 0 M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z",
  docs: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z M14 2v6h6 M8 13h8 M8 17h6",
  menu: "M3 6h18 M3 12h18 M3 18h18",
  close: "M6 6l12 12 M18 6 6 18",
  external: "M14 4h6v6 M20 4l-9 9 M19 13v6a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h6",
  chevron: "M6 9l6 6 6-6",
  sparkle: "M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8Z",
  shield: "M12 3l8 3v6c0 5-3.5 7.5-8 9-4.5-1.5-8-4-8-9V6Z",
};

type Props = {
  name: ShellIconName;
  size?: number;
  className?: string;
};

export function ShellIcon({ name, size = 18, className }: Props) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      fill="none"
      height={size}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth={1.7}
      viewBox="0 0 24 24"
      width={size}
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
