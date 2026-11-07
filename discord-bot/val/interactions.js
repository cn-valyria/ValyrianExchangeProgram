import { REGISTER_COMMAND, UNIQUE_CODE_COMMAND } from "./commands.js";
import { isValidSignature } from "./signature.js";

// https://discord.com/developers/docs/interactions/receiving-and-responding
const InteractionType = { PING: 1, APPLICATION_COMMAND: 2 };
const ResponseType = { PONG: 1, CHANNEL_MESSAGE_WITH_SOURCE: 4 };
const ComponentType = { ACTION_ROW: 1, BUTTON: 2 };
const ButtonStyle = { LINK: 5 };
const EPHEMERAL = 1 << 6;

export const NOT_REGISTERED_MESSAGE =
  "Your Discord account isn't registered for any VEP accounts. If you think this is a mistake, reach out to a VEP admin.";
export const LOOKUP_FAILED_MESSAGE = "Something went wrong looking up your unique code. Please try again in a minute.";
export const REGISTER_FAILED_MESSAGE = "Something went wrong creating your VEP link. Please try again in a minute.";
export const REGISTER_LINK_LABEL = "Link my VEP account";

const privateReply = (content, extra = {}) =>
  Response.json({
    type: ResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
    data: { content, flags: EPHEMERAL, allowed_mentions: { parse: [] }, ...extra },
  });

const linkButton = (label, url) => ({
  components: [{
    type: ComponentType.ACTION_ROW,
    components: [{ type: ComponentType.BUTTON, style: ButtonStyle.LINK, label, url }],
  }],
});

export function registerMessage(username) {
  return [
    `Link your Discord account (\`${username}\`) to your VEP account: open the link below, log into VEP if you aren't`,
    "already, and approve it. The link expires in 15 minutes. Don't share it with anyone.",
  ].join(" ");
}

function describeAccount(account) {
  const nation = account.nationName
    ? `${account.nationName} (ruled by ${account.rulerName}, nation ${account.nationId})`
    : `nation ${account.nationId}`;
  return `**${nation}**: \`${account.uniqueCode}\``;
}

export function uniqueCodeMessage(accounts) {
  if (accounts.length === 0) return NOT_REGISTERED_MESSAGE;
  if (accounts.length === 1) return `Your VEP unique code for ${describeAccount(accounts[0])}`;
  return ["Your VEP unique codes:", ...accounts.map((a) => `- ${describeAccount(a)}`)].join("\n");
}

// In a server the invoking user is on `member.user`, in a DM it's on `user`
const invokingUser = (interaction) => interaction.member?.user ?? interaction.user;

/**
 * @param {object} deps
 * @param {() => Promise<CryptoKey>} deps.getPublicKey
 * @param {(discordId: string) => Promise<Array<object>>} deps.findAccountsByDiscordId
 * @param {(user: {id: string, username: string}) => Promise<{url: string}>} deps.createRegisterLink
 */
export function createInteractionHandler({ getPublicKey, findAccountsByDiscordId, createRegisterLink }) {
  return async function handleInteraction(req) {
    if (req.method !== "POST") return new Response("Method not allowed", { status: 405 });

    const body = await req.text();
    const valid = await isValidSignature(
      await getPublicKey(),
      req.headers.get("X-Signature-Ed25519"),
      req.headers.get("X-Signature-Timestamp"),
      body,
    );
    if (!valid) return new Response("Invalid request signature", { status: 401 });

    const interaction = JSON.parse(body);

    if (interaction.type === InteractionType.PING) {
      return Response.json({ type: ResponseType.PONG });
    }

    if (
      interaction.type === InteractionType.APPLICATION_COMMAND &&
      interaction.data?.name === UNIQUE_CODE_COMMAND.name
    ) {
      const discordId = invokingUser(interaction)?.id;
      if (!discordId) return privateReply(NOT_REGISTERED_MESSAGE);
      try {
        return privateReply(uniqueCodeMessage(await findAccountsByDiscordId(discordId)));
      } catch (error) {
        console.error("Unique code lookup failed", error);
        return privateReply(LOOKUP_FAILED_MESSAGE);
      }
    }

    if (
      interaction.type === InteractionType.APPLICATION_COMMAND &&
      interaction.data?.name === REGISTER_COMMAND.name
    ) {
      const user = invokingUser(interaction);
      try {
        const { url } = await createRegisterLink({ id: user?.id, username: user?.username });
        return privateReply(registerMessage(user.username), linkButton(REGISTER_LINK_LABEL, url));
      } catch (error) {
        console.error("Creating the register link failed", error);
        return privateReply(REGISTER_FAILED_MESSAGE);
      }
    }

    return new Response("Unknown interaction", { status: 400 });
  };
}
