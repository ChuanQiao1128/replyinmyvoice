import { Card } from "../ui/card";

export function MetricCard({
  helper,
  label,
  value,
}: {
  label: string;
  value: string;
  helper?: string;
}) {
  return (
    <Card className="p-4">
      <p className="text-xs font-semibold uppercase tracking-[0.12em] text-ink/45">
        {label}
      </p>
      <p className="mt-2 text-2xl font-semibold text-ink">{value}</p>
      {helper ? <p className="mt-1 text-xs text-ink/50">{helper}</p> : null}
    </Card>
  );
}
