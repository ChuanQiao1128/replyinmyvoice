"use client";

type CsvDownloadOptions = {
  filename: string;
  path: string;
};

type DownloadErrorPayload = {
  detail?: string;
  error?: string | { message?: string };
  title?: string;
};

export async function downloadCsvFile({ filename, path }: CsvDownloadOptions) {
  const response = await fetch(path, {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return;
  }

  if (!response.ok) {
    throw new Error((await readDownloadError(response)) ?? "Could not export CSV.");
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);

  try {
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}

async function readDownloadError(response: Response) {
  const payload = (await response.json().catch(() => null)) as DownloadErrorPayload | null;
  if (typeof payload?.error === "string") {
    return payload.error;
  }

  return payload?.error?.message ?? payload?.detail ?? payload?.title;
}
