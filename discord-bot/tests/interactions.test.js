import { assertEquals, assertStringIncludes } from "jsr:@std/assert@1";
import {
  createInteractionHandler,
  LOOKUP_FAILED_MESSAGE,
  NOT_REGISTERED_MESSAGE,
  REGISTER_FAILED_MESSAGE,
  REGISTER_LINK_LABEL,
  registerMessage,
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

function handler(
  findAccountsByDiscordId = () => Promise.resolve([]),
  createRegisterLink = () => Promise.resolve({ url: "https://vep.example.com/discord/link?token=abc" }),
) {
  const lookups = [];
  const registrations = [];
  const handle = createInteractionHandler({
    getPublicKey: () => Promise.resolve(keyPair.publicKey),
    findAccountsByDiscordId: (id) => {
      lookups.push(id);
      return findAccountsByDiscordId(id);
    },
    createRegisterLink: (user) => {
      registrations.push(user);
      return createRegisterLink(user);
    },
  });
  return { handle, lookups, registrations };
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

const register = (user) => ({ type: 2, data: { name: "register" }, ...user });
const guildRegister = register({ member: { user: { id: "123456789012345678", username: "bob_" } }, guild_id: "1" });

Deno.test("/register in a server replies privately with a link button for the member", async () => {
  const { handle, registrations, lookups } = handler();
  const json = await (await handle(await signedRequest(guildRegister))).json();

  assertEquals(registrations, [{ id: "123456789012345678", username: "bob_" }]);
  assertEquals(lookups, []);
  assertEquals(json.type, 4);
  assertEquals(json.data.flags, 64);
  assertEquals(json.data.content, registerMessage("bob_"));
  assertStringIncludes(json.data.content, "`bob_`");
  assertEquals(json.data.components, [{
    type: 1,
    components: [{
      type: 2,
      style: 5,
      label: REGISTER_LINK_LABEL,
      url: "https://vep.example.com/discord/link?token=abc",
    }],
  }]);
});

Deno.test("/register in a DM uses the user", async () => {
  const { handle, registrations } = handler();
  await handle(await signedRequest(register({ user: { id: "987654321098765432", username: "sue" } })));
  assertEquals(registrations, [{ id: "987654321098765432", username: "sue" }]);
});

Deno.test("/register replies privately when the link can't be created", async () => {
  const { handle } = handler(undefined, () => Promise.reject(new Error("api down")));
  const originalError = console.error;
  console.error = () => {};
  try {
    const json = await (await handle(await signedRequest(guildRegister))).json();
    assertEquals(json.data.content, REGISTER_FAILED_MESSAGE);
    assertEquals(json.data.flags, 64);
    assertEquals(json.data.components, undefined);
  } finally {
    console.error = originalError;
  }
});
