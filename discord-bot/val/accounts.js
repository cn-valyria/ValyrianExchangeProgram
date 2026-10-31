// Discord gives up on an interaction after 3 seconds, so leave time to reply if the API is slow
const API_TIMEOUT_MS = 2500;

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
  if (!/^\d{1,19}$/.test(discordId)) return [];
  if (!apiUrl || !apiKey) throw new Error("VEP_API_URL and VEP_API_KEY must be set");

  const url = new URL(`api/discord/users/${discordId}/accounts`, apiUrl.endsWith("/") ? apiUrl : `${apiUrl}/`);
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
