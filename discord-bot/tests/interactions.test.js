import { assertEquals, assertStringIncludes } from "jsr:@std/assert@1";
import {
  createInteractionHandler,
  LOOKUP_FAILED_MESSAGE,
  NOT_REGISTERED_MESSAGE,
  uniqueCodeMessage,
} from "../val/interactions.js";

const keyPair = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
const toHex = (bytes) => Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, "0")).join("");

async function signedRequest(interaction, { privateKey = keyPair.privateKey, tamper = false } = {}) {
  const body = JSON.stringify(interaction);
  const timestamp = String(Math.floor(Date.now() / 1000));
  const signature = await crypto.subtle.sign("Ed25519", privateKey, new TextEncoder().encode(timestamp + body));
  return new Request("https://example.val.run/", {
    method: "POST",
    headers: { "X-Signature-Ed25519": toHex(signature), "X-Signature-Timestamp": timestamp },
    body: tamper ? body.replace("uniquecode", "uniquecodf") : body,
  });
}

function handler(findAccountsByDiscordId = () => Promise.resolve([])) {
  const lookups = [];
  const handle = createInteractionHandler({
    getPublicKey: () => Promise.resolve(keyPair.publicKey),
    findAccountsByDiscordId: (id) => {
      lookups.push(id);
      return findAccountsByDiscordId(id);
    },
  });
  return { handle, lookups };
}

const command = (user) => ({ type: 2, data: { name: "uniquecode" }, ...user });
const guildCommand = command({ member: { user: { id: "123456789012345678" } }, guild_id: "1" });

const bob = { nationId: "42", nationName: "Bobland", rulerName: "Bob", uniqueCode: "ABCDE" };
const bob2 = { nationId: "43", nationName: "Bobistan", rulerName: "Bob", uniqueCode: "QWERT" };

Deno.test("responds to Discord's PING with a PONG", async () => {
  const res = await handler().handle(await signedRequest({ type: 1 }));
  assertEquals(res.status, 200);
  assertEquals(await res.json(), { type: 1 });
});

Deno.test("rejects requests with a bad signature", async () => {
  const other = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
  const { handle, lookups } = handler();
  assertEquals((await handle(await signedRequest(guildCommand, { privateKey: other.privateKey }))).status, 401);
  assertEquals((await handle(await signedRequest(guildCommand, { tamper: true }))).status, 401);
  assertEquals(lookups, []);
});

Deno.test("rejects requests without signature headers", async () => {
  const req = new Request("https://example.val.run/", { method: "POST", body: JSON.stringify({ type: 1 }) });
  assertEquals((await handler().handle(req)).status, 401);
});

Deno.test("rejects non-POST requests", async () => {
  assertEquals((await handler().handle(new Request("https://example.val.run/"))).status, 405);
});

Deno.test("/uniquecode in a server looks up the member and replies privately with their code", async () => {
  const { handle, lookups } = handler(() => Promise.resolve([bob]));
  const res = await handle(await signedRequest(guildCommand));
  const json = await res.json();

  assertEquals(lookups, ["123456789012345678"]);
  assertEquals(json.type, 4);
  assertEquals(json.data.flags, 64);
  assertStringIncludes(json.data.content, "`ABCDE`");
  assertStringIncludes(json.data.content, "Bobland");
});

Deno.test("/uniquecode in a DM looks up the user", async () => {
  const { handle, lookups } = handler(() => Promise.resolve([bob]));
  await handle(await signedRequest(command({ user: { id: "987654321098765432" } })));
  assertEquals(lookups, ["987654321098765432"]);
});

Deno.test("/uniquecode lists every account linked to the Discord user", async () => {
  const { handle } = handler(() => Promise.resolve([bob, bob2]));
  const json = await (await handle(await signedRequest(guildCommand))).json();
  assertStringIncludes(json.data.content, "`ABCDE`");
  assertStringIncludes(json.data.content, "`QWERT`");
  assertEquals(json.data.flags, 64);
});

Deno.test("/uniquecode privately tells unregistered users they have no accounts", async () => {
  const json = await (await handler().handle(await signedRequest(guildCommand))).json();
  assertEquals(json.data, { content: NOT_REGISTERED_MESSAGE, flags: 64, allowed_mentions: { parse: [] } });
});

Deno.test("/uniquecode replies privately when the lookup fails", async () => {
  const { handle } = handler(() => Promise.reject(new Error("db down")));
  const originalError = console.error;
  console.error = () => {};
  try {
    const json = await (await handle(await signedRequest(guildCommand))).json();
    assertEquals(json.data.content, LOOKUP_FAILED_MESSAGE);
    assertEquals(json.data.flags, 64);
  } finally {
    console.error = originalError;
  }
});

Deno.test("unknown commands are rejected", async () => {
  const res = await handler().handle(await signedRequest({ type: 2, data: { name: "other" } }));
  assertEquals(res.status, 400);
});

Deno.test("uniqueCodeMessage falls back to the nation ID when the nation isn't loaded", () => {
  assertEquals(
    uniqueCodeMessage([{ nationId: "7", nationName: null, rulerName: null, uniqueCode: "ZXCVB" }]),
    "Your VEP unique code for **nation 7**: `ZXCVB`",
  );
});
