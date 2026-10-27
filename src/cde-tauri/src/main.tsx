import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { AppMantineProvider } from "./ui/mantine";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <AppMantineProvider>
      <App />
    </AppMantineProvider>
  </React.StrictMode>,
);
