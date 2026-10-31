import { findAccountsByDiscordId } from "./accounts.js";
import { createInteractionHandler } from "./interactions.js";
import { importPublicKey } from "./signature.js";

let publicKey;

// Val Town HTTP trigger: Discord's "Interactions Endpoint URL" points here
export default createInteractionHandler({
  getPublicKey: async () => (publicKey ??= await importPublicKey(Deno.env.get("DISCORD_PUBLIC_KEY"))),
  findAccountsByDiscordId,
});
