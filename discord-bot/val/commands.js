// Slash commands registered with Discord by scripts/register-commands.js
export const UNIQUE_CODE_COMMAND = {
  name: "uniquecode",
  type: 1, // CHAT_INPUT
  description: "Privately get the unique code for your VEP account(s)",
  integration_types: [0], // GUILD_INSTALL
  contexts: [0, 1], // GUILD, BOT_DM
};

export const COMMANDS = [UNIQUE_CODE_COMMAND];
