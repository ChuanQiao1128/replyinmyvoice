import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        ink: "#11150f",
        paper: "#f6f4ee",
        "paper-deep": "#e7e3d5",
        mint: "#d6e6dd",
        sky: "#e8f1ec",
        line: "#d8d4c4",
        clay: "#1e6b4a",
        sage: "#1e6b4a",
        gold: "#9a7b2e",
      },
      boxShadow: {
        soft: "0 18px 45px rgba(17, 21, 15, 0.10)",
        crisp: "0 12px 28px rgba(17, 21, 15, 0.08)",
      },
    },
  },
  plugins: [],
};

export default config;
