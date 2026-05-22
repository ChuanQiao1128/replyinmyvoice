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
        ink: "#211f1b",
        paper: "#f8f2e8",
        cream: "#fffaf1",
        "paper-deep": "#eadfce",
        line: "#d8c9b6",
        clay: "#9a5534",
        brick: "#a34832",
        sage: "#667861",
        evergreen: "#264f49",
        mist: "#dce8e3",
        gold: "#c2903c",
      },
      boxShadow: {
        soft: "0 18px 45px rgba(49, 40, 29, 0.10)",
        panel: "0 16px 38px rgba(38, 79, 73, 0.12)",
      },
    },
  },
  plugins: [],
};

export default config;
