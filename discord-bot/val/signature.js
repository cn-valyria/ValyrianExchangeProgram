// Discord signs every interaction request with the application's Ed25519 key:
// https://discord.com/developers/docs/interactions/overview#preparing-for-interactions
const encoder = new TextEncoder();

function hexToBytes(hex) {
  if (!/^(?:[0-9a-fA-F]{2})+$/.test(hex)) return null;
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < bytes.length; i++) {
    bytes[i] = parseInt(hex.slice(i * 2, i * 2 + 2), 16);
  }
  return bytes;
}

export async function importPublicKey(publicKeyHex) {
  const keyBytes = hexToBytes(publicKeyHex ?? "");
  if (!keyBytes) throw new Error("DISCORD_PUBLIC_KEY is missing or not hex");
  return await crypto.subtle.importKey("raw", keyBytes, { name: "Ed25519" }, false, ["verify"]);
}

export async function isValidSignature(publicKey, signatureHex, timestamp, body) {
  const signature = hexToBytes(signatureHex ?? "");
  if (!signature || !timestamp) return false;
  try {
    return await crypto.subtle.verify("Ed25519", publicKey, signature, encoder.encode(timestamp + body));
  } catch {
    return false;
  }
}
