import disposableEmailDomains from "./disposable-email-domains.json";

const listedDomains = new Set(disposableEmailDomains);

export function emailDomain(email: string): string | null {
  const normalized = email.trim().toLowerCase();
  const atIndex = normalized.lastIndexOf("@");
  if (atIndex <= 0 || atIndex === normalized.length - 1) {
    return null;
  }

  return normalized.slice(atIndex + 1);
}

export function isDisposableEmail(email: string): boolean {
  const domain = emailDomain(email);
  return Boolean(domain && isDisposableEmailDomain(domain));
}

export function isDisposableEmailDomain(domain: string): boolean {
  const normalized = domain.trim().toLowerCase().replace(/\.$/, "");
  if (!normalized || normalized.includes("@")) {
    return false;
  }

  let candidate = normalized;
  while (candidate) {
    if (listedDomains.has(candidate)) {
      return true;
    }

    const dotIndex = candidate.indexOf(".");
    if (dotIndex < 0) {
      return false;
    }
    candidate = candidate.slice(dotIndex + 1);
  }

  return false;
}
