import mysql from "npm:mysql2@3/promise";

let pool;

function getPool() {
  pool ??= mysql.createPool({
    host: Deno.env.get("VEP_DB_HOST"),
    port: Number(Deno.env.get("VEP_DB_PORT") ?? 3306),
    user: Deno.env.get("VEP_DB_USER"),
    password: Deno.env.get("VEP_DB_PASSWORD"),
    // Azure Database for MySQL requires TLS; set VEP_DB_SSL=false only for a local database
    ssl: Deno.env.get("VEP_DB_SSL") === "false" ? undefined : { rejectUnauthorized: true },
    connectionLimit: 2,
    connectTimeout: 2500, // Discord gives up on an interaction after 3 seconds
    supportBigNumbers: true,
    bigNumberStrings: true,
  });
  return pool;
}

// Discord IDs are snowflakes larger than 2^53, so the ID is passed as a string and cast to an
// integer in MySQL rather than letting a string-to-bigint comparison fall back to doubles
const FIND_ACCOUNTS_BY_DISCORD_ID = `
select account.nation_id as nationId,
    nation.nation_name as nationName,
    nation.ruler_name as rulerName,
    account.psw as uniqueCode
from vep_db.account
left join cybernations_db.nation on nation.id = account.nation_id
where account.discord_id = cast(? as unsigned)
order by account.nation_id`;

export async function findAccountsByDiscordId(discordId) {
  if (!/^\d{1,20}$/.test(discordId)) return [];
  const [rows] = await getPool().query(FIND_ACCOUNTS_BY_DISCORD_ID, [discordId]);
  return rows.map((row) => ({ ...row, nationId: String(row.nationId) }));
}
