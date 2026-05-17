import { copyFileSync, existsSync, mkdirSync, readdirSync } from "node:fs";
import { join } from "node:path";

const sourceDir = join(
  process.cwd(),
  ".next",
  "server",
  "chunks",
  "static",
  "wasm",
);
const targetDir = join(process.cwd(), ".open-next", "static", "wasm");

if (!existsSync(sourceDir)) {
  console.log("No Prisma WASM artifacts found to copy.");
  process.exit(0);
}

const wasmFiles = readdirSync(sourceDir).filter((file) => file.endsWith(".wasm"));

if (wasmFiles.length === 0) {
  console.log("No Prisma WASM artifacts found to copy.");
  process.exit(0);
}

mkdirSync(targetDir, { recursive: true });

for (const file of wasmFiles) {
  copyFileSync(join(sourceDir, file), join(targetDir, file));
}

console.log(`Copied ${wasmFiles.length} Prisma WASM artifact(s) for Cloudflare.`);
