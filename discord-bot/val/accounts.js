// Discord gives up on an interaction after 3 seconds, so leave time to reply if the API is slow
const API_TIMEOUT_MS = 2500;

const DISCORD_ID = /^\d{1,19}$/;

function apiEndpoint(path, { apiUrl, apiKey }) {
  if (!apiUrl || !apiKey) throw new Error("VEP_API_URL and VEP_API_KEY must be set");
  return new URL(path, apiUrl.endsWith("/") ? apiUrl : `${apiUrl}/`);
}

/**
 * Looks up the VEP accounts linked to a Discord user through the VEP API, authenticated with the API key that's also
 * the API's DiscordBotApiKey setting.
 *
 * Discord IDs are snowflakes larger than 2^53, so they stay strings and go straight into the URL.
 */
export async function findAccountsByDiscordId(discordId, {
  apiUrl = Deno.env.get("VEP_API_URL"),
  apiKey = Deno.env.get("VEP_API_KEY"),
  fetch = globalThis.fetch,
} = {}) {
  if (!DISCORD_ID.test(discordId)) return [];

  const url = apiEndpoint(`api/discord/users/${discordId}/accounts`, { apiUrl, apiKey });
  const response = await fetch(url, {
    headers: { "X-Api-Key": apiKey, Accept: "application/json" },
    signal: AbortSignal.timeout(API_TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`VEP API returned ${response.status} for the Discord account lookup`);

  const accounts = await response.json();
  return accounts.map(({ nationId, nationName, rulerName, uniqueCode }) => ({
    nationId: String(nationId),
    nationName,
    rulerName,
    uniqueCode,
  }));
}

/**
 * Builds the /register link for a Discord user: the VEP site's /discord/link page, with a short-lived token the VEP
 * API signs for this Discord ID and username. The page only links the Discord account in the token, and only to the
 * VEP account the member is logged into, so nobody can link a Discord account they didn't run /register from.
 */
export async function createRegisterLink({ id, username }, {
  apiUrl = Deno.env.get("VEP_API_URL"),
  apiKey = Deno.env.get("VEP_API_KEY"),
  uiUrl = Deno.env.get("VEP_UI_URL"),
  fetch = globalThis.fetch,
} = {}) {
  if (!DISCORD_ID.test(id ?? "") || !username) throw new Error("A Discord user ID and username are required");
  if (!uiUrl) throw new Error("VEP_UI_URL must be set");

  const response = await fetch(apiEndpoint("api/discord/link-tokens", { apiUrl, apiKey }), {
    method: "POST",
    headers: { "X-Api-Key": apiKey, Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify({ discordId: id, discordUsername: username }),
    signal: AbortSignal.timeout(API_TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`VEP API returned ${response.status} for the Discord link token`);

  const { token, expiresAt } = await response.json();
  const url = new URL("discord/link", uiUrl.endsWith("/") ? uiUrl : `${uiUrl}/`);
  url.searchParams.set("token", token);
  return { url: url.href, expiresAt };
}
