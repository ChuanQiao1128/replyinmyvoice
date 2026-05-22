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
        ink: "#22201c",
        paper: "#fbf7ef",
        "paper-deep": "#f2eadc",
        mint: "#edf6f0",
        sky: "#edf5f8",
        line: "#ded2bf",
        clay: "#9c5f3b",
        sage: "#5f7466",
        gold: "#c49445",
      },
      boxShadow: {
        soft: "0 18px 45px rgba(56, 45, 32, 0.10)",
        crisp: "0 12px 28px rgba(34, 32, 28, 0.08)",
      },
    },
  },
  plugins: [],
};

export default config;
