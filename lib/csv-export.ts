export function serializeCsv(
  columns: readonly string[],
  rows: readonly Record<string, unknown>[],
) {
  return [
    columns.map(escapeCsvCell).join(","),
    ...rows.map((row) => columns.map((column) => escapeCsvCell(row[column])).join(",")),
  ].join("\n");
}

function escapeCsvCell(value: unknown) {
  if (value === null || value === undefined) {
    return "";
  }

  const text = neutralizeSpreadsheetFormula(String(value));
  if (!/[",\r\n\t]/.test(text)) {
    return text;
  }

  return `"${text.replaceAll('"', '""')}"`;
}

function neutralizeSpreadsheetFormula(value: string) {
  return /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
}
