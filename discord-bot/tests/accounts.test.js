import { assertEquals, assertRejects } from "jsr:@std/assert@1";
import { createRegisterLink, findAccountsByDiscordId } from "../val/accounts.js";

function fakeFetch(response) {
  const calls = [];
  const fetch = (url, init) => {
    calls.push({ url: String(url), init });
    return Promise.resolve(typeof response === "function" ? response() : response);
  };
  return { fetch, calls };
}

const config = (fetch, apiUrl = "https://api.example.com") => ({ apiUrl, apiKey: "secret-key", fetch });

Deno.test("calls the VEP API with the API key and maps the accounts", async () => {
  const { fetch, calls } = fakeFetch(Response.json([
    { nationId: 42, nationName: "Bobland", rulerName: "Bob", uniqueCode: "ABCDE" },
    { nationId: 43, nationName: null, rulerName: null, uniqueCode: "QWERT" },
  ]));

  const accounts = await findAccountsByDiscordId("123456789012345678", config(fetch));

  assertEquals(calls.length, 1);
  assertEquals(calls[0].url, "https://api.example.com/api/discord/users/123456789012345678/accounts");
  assertEquals(calls[0].init.headers["X-Api-Key"], "secret-key");
  assertEquals(accounts, [
    { nationId: "42", nationName: "Bobland", rulerName: "Bob", uniqueCode: "ABCDE" },
    { nationId: "43", nationName: null, rulerName: null, uniqueCode: "QWERT" },
  ]);
});

Deno.test("works with or without a trailing slash on VEP_API_URL", async () => {
  for (const apiUrl of ["https://api.example.com/", "https://api.example.com"]) {
    const { fetch, calls } = fakeFetch(() => Response.json([]));
    assertEquals(await findAccountsByDiscordId("1", config(fetch, apiUrl)), []);
    assertEquals(calls[0].url, "https://api.example.com/api/discord/users/1/accounts");
  }
});

Deno.test("returns nothing for an ID that isn't a snowflake, without calling the API", async () => {
  const { fetch, calls } = fakeFetch(Response.json([]));
  for (const id of ["", "abc", "1/../2", "12345678901234567890", undefined]) {
    assertEquals(await findAccountsByDiscordId(id, config(fetch)), []);
  }
  assertEquals(calls, []);
});

Deno.test("throws when the API rejects the request", async () => {
  const { fetch } = fakeFetch(new Response(null, { status: 401 }));
  await assertRejects(() => findAccountsByDiscordId("1", config(fetch)), Error, "401");
});

Deno.test("throws when the API isn't configured", async () => {
  const { fetch, calls } = fakeFetch(Response.json([]));
  await assertRejects(() => findAccountsByDiscordId("1", { apiUrl: "", apiKey: "", fetch }), Error, "VEP_API_URL");
  assertEquals(calls, []);
});

const linkConfig = (fetch, uiUrl = "https://vep.example.com") => ({ ...config(fetch), uiUrl });
const bob = { id: "123456789012345678", username: "bob_" };

Deno.test("createRegisterLink gets a link token from the VEP API and links to the VEP link page", async () => {
  const { fetch, calls } = fakeFetch(Response.json({ token: "a.b+c", expiresAt: "2026-11-07T16:15:00Z" }));

  const link = await createRegisterLink(bob, linkConfig(fetch));

  assertEquals(calls.length, 1);
  assertEquals(calls[0].url, "https://api.example.com/api/discord/link-tokens");
  assertEquals(calls[0].init.method, "POST");
  assertEquals(calls[0].init.headers["X-Api-Key"], "secret-key");
  assertEquals(JSON.parse(calls[0].init.body), { discordId: "123456789012345678", discordUsername: "bob_" });
  assertEquals(link, {
    url: "https://vep.example.com/discord/link?token=a.b%2Bc",
    expiresAt: "2026-11-07T16:15:00Z",
  });
});

Deno.test("createRegisterLink works with or without a trailing slash on VEP_UI_URL", async () => {
  for (const uiUrl of ["https://vep.example.com/", "https://vep.example.com"]) {
    const { fetch } = fakeFetch(() => Response.json({ token: "t" }));
    const { url } = await createRegisterLink(bob, linkConfig(fetch, uiUrl));
    assertEquals(url, "https://vep.example.com/discord/link?token=t");
  }
});

Deno.test("createRegisterLink rejects a bad user or missing config without calling the API", async () => {
  const { fetch, calls } = fakeFetch(Response.json({ token: "t" }));
  await assertRejects(() => createRegisterLink({ id: "abc", username: "bob" }, linkConfig(fetch)), Error);
  await assertRejects(() => createRegisterLink({ id: "1", username: "" }, linkConfig(fetch)), Error);
  await assertRejects(() => createRegisterLink(bob, linkConfig(fetch, "")), Error, "VEP_UI_URL");
  await assertRejects(
    () => createRegisterLink(bob, { apiUrl: "", apiKey: "", uiUrl: "https://vep.example.com", fetch }),
    Error,
    "VEP_API_URL",
  );
  assertEquals(calls, []);
});

Deno.test("createRegisterLink throws when the API rejects the request", async () => {
  const { fetch } = fakeFetch(new Response(null, { status: 401 }));
  await assertRejects(() => createRegisterLink(bob, linkConfig(fetch)), Error, "401");
});
