# VEP Discord bot

A Discord bot, hosted on [Val Town](https://www.val.town), that lets members privately look up their VEP account's
unique code.

It's deliberately separate from the .NET Aspire app: it's a small JavaScript (Deno) HTTP handler that Val Town runs for
free, and it reads straight from the VEP MySQL database.

## Commands

| Command       | What it does                                                                                                                                                                                                                                            |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/uniquecode` | Looks up every `vep_db.account` row whose `discord_id` matches the Discord user running the command and replies with each account's unique code (`account.psw`). If nothing matches, it says the Discord account isn't registered for any VEP accounts. |

Every reply is [ephemeral](https://support.discord.com/hc/en-us/articles/1500000580222-Ephemeral-Messages-FAQ): only the
person who ran the command can see it. The command works in any server the bot is in, and in a DM with the bot.

## How it works

Discord [interactions](https://discord.com/developers/docs/interactions/receiving-and-responding) are delivered as HTTP
POSTs to the bot's **Interactions Endpoint URL**, which points at the val's HTTP trigger. There's no gateway connection
or always-on process.

```
Discord ──POST /──► Val Town (main.http.js) ──SELECT──► Azure MySQL (vep_db.account + cybernations_db.nation)
        ◄─ephemeral reply──
```

1. `main.http.js` verifies the request's Ed25519 signature against `DISCORD_PUBLIC_KEY` (Discord requires this and
   probes the endpoint with bad signatures) and rejects anything unsigned with a 401.
2. Discord's `PING` gets a `PONG`, which is how Discord validates the endpoint URL.
3. `/uniquecode` takes the invoking user's ID (`member.user.id` in a server, `user.id` in a DM), queries the database,
   and responds with an ephemeral message.

Discord gives up on an interaction after 3 seconds, so the database connection times out after 2.5 seconds and the bot
replies with a "try again" message if the lookup fails. The connection pool is kept between requests while Val Town
keeps the val warm.

### Layout

| Path                           | Purpose                                                                                     |
| ------------------------------ | ------------------------------------------------------------------------------------------- |
| `val/`                         | Everything deployed to Val Town (mirrored to the val on every deploy)                       |
| `val/main.http.js`             | The val's HTTP trigger. The `http` in the file name is what makes Val Town create it as one |
| `val/interactions.js`          | Signature check, interaction routing, and reply formatting                                  |
| `val/accounts.js`              | The MySQL lookup (`npm:mysql2`)                                                             |
| `val/signature.js`             | Ed25519 request verification with Web Crypto                                                |
| `val/commands.js`              | Slash command definitions                                                                   |
| `scripts/register-commands.js` | Registers `val/commands.js` with Discord (bulk overwrite, safe to rerun)                    |
| `scripts/serve-local.js`       | Serves the handler locally (`deno task serve`)                                              |
| `tests/`                       | Deno tests                                                                                  |

Discord IDs are bigger than 2^53, so they're always handled as strings and cast to an integer in SQL rather than
compared as doubles.

## Deployment

The `deploy-discord-bot` job in [`.github/workflows/ci-cd.yml`](../.github/workflows/ci-cd.yml) runs on every push to
`main` (after `discord-bot-tests` passes) and:

1. Clones the val with the [vt CLI](https://github.com/val-town/vt), mirrors `discord-bot/val/` into it, and runs
   `vt push`. The repo is the source of truth: edits made in the Val Town web editor are overwritten, and files that
   only exist in the val are deleted.
2. Runs `deno task register` to register the slash commands with Discord.

It's independent of the Aspire deploy, so a failure in one doesn't block the other.

### GitHub configuration (`production` environment)

| Name                     | Kind     | Value                                                                                                         |
| ------------------------ | -------- | ------------------------------------------------------------------------------------------------------------- |
| `VAL_TOWN_API_KEY`       | secret   | Val Town API key with val read + write scope, from [val.town/settings/api](https://www.val.town/settings/api) |
| `VAL_TOWN_VAL`           | variable | The val as `username/valName`, e.g. `lilweirdward/vepDiscordBot`                                              |
| `DISCORD_APPLICATION_ID` | variable | Application ID from the Discord Developer Portal's General Information page                                   |
| `DISCORD_BOT_TOKEN`      | secret   | Bot token from the Discord Developer Portal's Bot page                                                        |

### Val Town environment variables

Set these on the val (left sidebar → Environment variables). They're only read at runtime, never committed, and changes
apply on the next request without a redeploy.

| Name                 | Value                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------- |
| `DISCORD_PUBLIC_KEY` | Public key from the Discord Developer Portal's General Information page               |
| `VEP_DB_HOST`        | MySQL host, e.g. `<server>.mysql.database.azure.com`                                  |
| `VEP_DB_PORT`        | Optional, defaults to `3306`                                                          |
| `VEP_DB_USER`        | The bot's read-only database user (see below)                                         |
| `VEP_DB_PASSWORD`    | That user's password                                                                  |
| `VEP_DB_SSL`         | Optional. TLS is on by default (Azure requires it); `false` only for a local database |

## One-time setup

1. **Create the Discord application** in the [Developer Portal](https://discord.com/developers/applications). No
   privileged intents are needed. Invite it to the server from **OAuth2 → URL Generator** with the
   `applications.commands` and `bot` scopes (no extra permissions are needed, since it only replies to its own command).
2. **Create the val** at [val.town](https://www.val.town) (or `vt create vepDiscordBot`). It can start empty; the first
   deploy adds the files. Free accounts can only create public vals. That's fine here, since the code is already public
   in this repo and environment variables are never visible to others.
3. **Create a read-only database user** that can only see the columns the bot needs:
   ```sql
   CREATE USER 'vep_discord_bot'@'%' IDENTIFIED BY '<strong password>';
   GRANT SELECT (nation_id, discord_id, psw) ON vep_db.account TO 'vep_discord_bot'@'%';
   GRANT SELECT (id, nation_name, ruler_name) ON cybernations_db.nation TO 'vep_discord_bot'@'%';
   ```
4. **Allow Val Town through the MySQL firewall.** Val Town's
   [outbound IP ranges](https://docs.val.town/reference/runtime/) are `74.220.50.0/24` and `74.220.58.0/24`:
   ```bash
   az mysql flexible-server firewall-rule create --resource-group <rg> --name <server> \
     --rule-name val-town-1 --start-ip-address 74.220.50.0 --end-ip-address 74.220.50.255
   az mysql flexible-server firewall-rule create --resource-group <rg> --name <server> \
     --rule-name val-town-2 --start-ip-address 74.220.58.0 --end-ip-address 74.220.58.255
   ```
   These ranges are shared by every val on Val Town, so the database password and the column-limited grants above are
   what actually protect the data.
5. **Set the GitHub and Val Town configuration** from the tables above, then merge to `main` (or run the CI/CD workflow
   manually) to deploy.
6. **Point Discord at the val:** copy the val's HTTP endpoint URL (e.g. `https://<username>--<id>.web.val.run`) into
   **Interactions Endpoint URL** on the Developer Portal's General Information page and save. Discord sends a signed
   `PING` to check it, so this only succeeds once the val is deployed and `DISCORD_PUBLIC_KEY` is set.

## Local development

Requires [Deno](https://docs.deno.com/runtime/getting_started/installation/) 2.

```bash
cd discord-bot
deno task check      # fmt --check, lint, and tests (what CI runs)
deno fmt             # fix formatting
deno test            # tests only

# Register the slash commands by hand
DISCORD_APPLICATION_ID=... DISCORD_BOT_TOKEN=... deno task register
```

To serve the handler locally against a database (e.g. a local MySQL with `VEP_DB_SSL=false`):

```bash
VEP_DB_HOST=127.0.0.1 VEP_DB_USER=... VEP_DB_PASSWORD=... VEP_DB_SSL=false DISCORD_PUBLIC_KEY=... deno task serve
```

Discord can only reach a public URL, so end-to-end testing against Discord is easiest on a
[branch of the val](https://docs.val.town/vals/branches/) with its own test application.

## Adding a command

1. Add its definition to `val/commands.js`.
2. Handle it in `val/interactions.js` and add tests in `tests/`.
3. Merge to `main`; the deploy re-registers the commands.
