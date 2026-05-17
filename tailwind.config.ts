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
        line: "#ded2bf",
        clay: "#9c5f3b",
        sage: "#5f7466",
        gold: "#c49445",
      },
      boxShadow: {
        soft: "0 18px 45px rgba(56, 45, 32, 0.10)",
      },
    },
  },
  plugins: [],
};

export default config;
