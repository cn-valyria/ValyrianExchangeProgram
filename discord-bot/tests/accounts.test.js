import { assertEquals, assertRejects } from "jsr:@std/assert@1";
import { findAccountsByDiscordId } from "../val/accounts.js";

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
