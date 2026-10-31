// Serves the val's HTTP handler locally, e.g. against a local MySQL. Usage: deno task serve
import handler from "../val/main.http.js";

Deno.serve({ port: Number(Deno.env.get("PORT") ?? 8000) }, handler);
