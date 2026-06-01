import { createHmac, timingSafeEqual } from "node:crypto";
import { createServer, type IncomingMessage, type ServerResponse } from "node:http";

type UserState = {
  email: string | null;
  credits: CreditState[];
};

type CreditState = {
  amountConsumed: number;
  amountGranted: number;
  amountTotal: number;
  paymentIntentId: string;
  sku: string;
};

type RequestUser = {
  email: string | null;
  sub: string;
};

const freeQuota = 3;
const quickPackRewrites = 10;
const users = new Map<string, UserState>();
const processedEvents = new Set<string>();

function json(response: ServerResponse, status: number, body: unknown) {
  response.writeHead(status, {
    "content-type": "application/json",
  });
  response.end(JSON.stringify(body));
}

function problem(response: ServerResponse, status: number, message: string) {
  json(response, status, { error: message });
}

async function readBody(request: IncomingMessage) {
  const chunks: Buffer[] = [];
  for await (const chunk of request) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }
  return Buffer.concat(chunks).toString("utf8");
}

function decodeBase64UrlJson(value: string): Record<string, unknown> | null {
  try {
    return JSON.parse(Buffer.from(value, "base64url").toString("utf8")) as Record<
      string,
      unknown
    >;
  } catch {
    return null;
  }
}

function userFromAuthorization(request: IncomingMessage): RequestUser | null {
  const authorization = request.headers.authorization;
  if (!authorization?.startsWith("Bearer ")) {
    return null;
  }

  const token = authorization.slice("Bearer ".length).trim();
  const [, payload] = token.split(".");
  const claims = payload ? decodeBase64UrlJson(payload) : null;
  const sub = typeof claims?.sub === "string" ? claims.sub.trim() : "";
  if (!sub) {
    return null;
  }

  return {
    email: typeof claims?.email === "string" ? claims.email : null,
    sub,
  };
}

function getOrCreateUser(user: RequestUser) {
  const existing = users.get(user.sub);
  if (existing) {
    if (user.email) {
      existing.email = user.email;
    }
    return existing;
  }

  const created = {
    credits: [],
    email: user.email,
  };
  users.set(user.sub, created);
  return created;
}

function accountSummary(user: RequestUser) {
  const state = getOrCreateUser(user);
  const creditRemaining = state.credits.reduce(
    (total, credit) =>
      total + Math.max(credit.amountGranted - credit.amountConsumed, 0),
    0,
  );
  const quota = freeQuota + creditRemaining;

  return {
    currentPeriodEnd: null,
    email: state.email,
    externalAuthUserId: user.sub,
    subscriptionStatus: "Inactive",
    usage: {
      exhausted: quota <= 0,
      periodEnd: null,
      periodKey: "free:lifetime",
      quota,
      remaining: quota,
      reserved: 0,
      scope: "free",
      sources: [
        {
          expiresAt: null,
          expiresInDays: null,
          label: "Free rewrites",
          quota: freeQuota,
          remaining: freeQuota,
          reserved: 0,
          source: "free",
          used: 0,
        },
        ...state.credits.map((credit) => ({
          expiresAt: null,
          expiresInDays: null,
          label: "PURCHASE",
          quota: credit.amountGranted,
          remaining: Math.max(credit.amountGranted - credit.amountConsumed, 0),
          reserved: 0,
          source: "PURCHASE",
          used: credit.amountConsumed,
        })),
      ],
      used: 0,
    },
    userId: user.sub,
  };
}

function verifyStripeSignature(rawBody: string, signatureHeader: string | undefined) {
  const secret = process.env.PAYMENT_E2E_STRIPE_WEBHOOK_SECRET;
  if (!secret) {
    return false;
  }

  const parts = new Map(
    (signatureHeader ?? "")
      .split(",")
      .map((part) => part.split("="))
      .filter((part): part is [string, string] => part.length === 2),
  );
  const timestamp = parts.get("t");
  const signature = parts.get("v1");
  if (!timestamp || !signature) {
    return false;
  }

  const expected = createHmac("sha256", secret)
    .update(`${timestamp}.${rawBody}`)
    .digest("hex");
  const left = Buffer.from(signature, "hex");
  const right = Buffer.from(expected, "hex");
  return left.length === right.length && timingSafeEqual(left, right);
}

function recordCheckoutSession(stripeObject: Record<string, unknown>) {
  const metadata =
    stripeObject.metadata &&
    typeof stripeObject.metadata === "object" &&
    !Array.isArray(stripeObject.metadata)
      ? (stripeObject.metadata as Record<string, unknown>)
      : {};
  const externalAuthUserId =
    typeof stripeObject.client_reference_id === "string"
      ? stripeObject.client_reference_id
      : typeof metadata.externalAuthUserId === "string"
        ? metadata.externalAuthUserId
        : "";
  if (!externalAuthUserId || !users.has(externalAuthUserId)) {
    return false;
  }

  const state = users.get(externalAuthUserId)!;
  const paymentIntentId =
    typeof stripeObject.payment_intent === "string"
      ? stripeObject.payment_intent
      : "pi_playwright_quick_pack";
  if (state.credits.some((credit) => credit.paymentIntentId === paymentIntentId)) {
    return true;
  }

  state.credits.push({
    amountConsumed: 0,
    amountGranted: Number(metadata.rewrites) || quickPackRewrites,
    amountTotal:
      typeof stripeObject.amount_total === "number" ? stripeObject.amount_total : 250,
    paymentIntentId,
    sku: typeof metadata.sku === "string" ? metadata.sku : "quick_pack",
  });
  return true;
}

function recordChargeRefund(stripeObject: Record<string, unknown>) {
  const paymentIntentId =
    typeof stripeObject.payment_intent === "string" ? stripeObject.payment_intent : "";
  if (!paymentIntentId) {
    return false;
  }

  let found = false;
  for (const state of users.values()) {
    for (const credit of state.credits) {
      if (credit.paymentIntentId !== paymentIntentId) {
        continue;
      }
      found = true;
      const amount =
        typeof stripeObject.amount === "number" ? stripeObject.amount : credit.amountTotal;
      const amountRefunded =
        typeof stripeObject.amount_refunded === "number"
          ? stripeObject.amount_refunded
          : amount;
      const fullyRefunded = stripeObject.refunded === true || amountRefunded >= amount;
      if (fullyRefunded) {
        credit.amountGranted = credit.amountConsumed;
      } else {
        const refundedCredits = Math.ceil(
          quickPackRewrites * Math.min(amountRefunded, amount) / amount,
        );
        credit.amountGranted = Math.max(
          credit.amountConsumed,
          quickPackRewrites - refundedCredits,
        );
      }
    }
  }
  return found;
}

async function handleWebhook(request: IncomingMessage, response: ServerResponse) {
  const rawBody = await readBody(request);
  if (!verifyStripeSignature(rawBody, request.headers["stripe-signature"] as string | undefined)) {
    problem(response, 400, "Invalid Stripe signature.");
    return;
  }

  const event = JSON.parse(rawBody) as {
    data?: { object?: Record<string, unknown> };
    id?: string;
    type?: string;
  };
  if (!event.id || !event.type) {
    problem(response, 400, "Event id and type are required.");
    return;
  }
  if (processedEvents.has(event.id)) {
    json(response, 200, { processed: false, received: true });
    return;
  }

  const stripeObject = event.data?.object ?? {};
  const processed =
    event.type === "checkout.session.completed"
      ? recordCheckoutSession(stripeObject)
      : event.type === "charge.refunded"
        ? recordChargeRefund(stripeObject)
        : true;

  if (processed) {
    processedEvents.add(event.id);
  }
  json(response, 200, { processed, received: true });
}

async function handleRequest(request: IncomingMessage, response: ServerResponse) {
  const url = new URL(request.url ?? "/", "http://127.0.0.1");

  try {
    if (request.method === "GET" && url.pathname === "/__health") {
      json(response, 200, { ok: true });
      return;
    }

    if (request.method === "POST" && url.pathname === "/__reset") {
      users.clear();
      processedEvents.clear();
      response.writeHead(204);
      response.end();
      return;
    }

    if (request.method === "GET" && url.pathname === "/api/me") {
      const user = userFromAuthorization(request);
      if (!user) {
        problem(response, 401, "Authentication required.");
        return;
      }
      json(response, 200, accountSummary(user));
      return;
    }

    if (request.method === "POST" && url.pathname === "/api/stripe/checkout") {
      const user = userFromAuthorization(request);
      if (!user) {
        problem(response, 401, "Authentication required.");
        return;
      }
      getOrCreateUser(user);
      json(response, 200, {
        url: "https://checkout.stripe.com/c/pay/cs_test_playwright_quick_pack",
      });
      return;
    }

    if (request.method === "POST" && url.pathname === "/api/stripe/webhook") {
      await handleWebhook(request, response);
      return;
    }

    problem(response, 404, "Not found.");
  } catch (error) {
    problem(
      response,
      500,
      error instanceof Error ? error.message : "Payment E2E mock failed.",
    );
  }
}

const port = Number(process.env.PAYMENT_E2E_MOCK_API_PORT ?? "43183");
const server = createServer((request, response) => {
  void handleRequest(request, response);
});

server.listen(port, "127.0.0.1");

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => {
    server.close(() => process.exit(0));
  });
}
