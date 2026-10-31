// Registers the bot's slash commands with Discord. Bulk overwrite, so it's safe to rerun and
// removes any commands no longer in COMMANDS.
// Usage: DISCORD_APPLICATION_ID=... DISCORD_BOT_TOKEN=... deno task register
import { COMMANDS } from "../val/commands.js";

const applicationId = Deno.env.get("DISCORD_APPLICATION_ID");
const botToken = Deno.env.get("DISCORD_BOT_TOKEN");
if (!applicationId || !botToken) {
  console.error("DISCORD_APPLICATION_ID and DISCORD_BOT_TOKEN must be set");
  Deno.exit(1);
}

const response = await fetch(`https://discord.com/api/v10/applications/${applicationId}/commands`, {
  method: "PUT",
  headers: { "Authorization": `Bot ${botToken}`, "Content-Type": "application/json" },
  body: JSON.stringify(COMMANDS),
});

if (!response.ok) {
  console.error(`Registering commands failed: ${response.status} ${await response.text()}`);
  Deno.exit(1);
}

const registered = await response.json();
console.log(`Registered commands: ${registered.map((c) => `/${c.name}`).join(", ")}`);
