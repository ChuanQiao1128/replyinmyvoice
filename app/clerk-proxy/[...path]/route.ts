const CLERK_FRONTEND_API_URL =
  process.env.CLERK_FRONTEND_API_URL || "https://clerk.replyinmyvoice.com";

export const dynamic = "force-dynamic";

type ClerkProxyContext = {
  params: Promise<{
    path?: string[];
  }>;
};

function copyRequestHeaders(request: Request) {
  const headers = new Headers(request.headers);
  headers.delete("host");
  headers.delete("connection");
  headers.delete("content-length");

  return headers;
}

function copyResponseHeaders(response: Response) {
  const headers = new Headers(response.headers);
  headers.delete("content-encoding");
  headers.delete("content-length");
  headers.delete("transfer-encoding");
  headers.delete("connection");

  return headers;
}

async function proxyClerkRequest(request: Request, context: ClerkProxyContext) {
  const { path = [] } = await context.params;
  const inboundUrl = new URL(request.url);
  const upstreamPath = path.map(encodeURIComponent).join("/");
  const upstreamUrl = new URL(`/${upstreamPath}`, CLERK_FRONTEND_API_URL);
  upstreamUrl.search = inboundUrl.search;

  const method = request.method.toUpperCase();
  const hasBody = !["GET", "HEAD"].includes(method);
  const response = await fetch(upstreamUrl, {
    method,
    headers: copyRequestHeaders(request),
    body: hasBody ? await request.arrayBuffer() : undefined,
    redirect: "manual",
  });

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: copyResponseHeaders(response),
  });
}

export function GET(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}

export function HEAD(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}

export function POST(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}

export function PUT(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}

export function PATCH(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}

export function DELETE(request: Request, context: ClerkProxyContext) {
  return proxyClerkRequest(request, context);
}
