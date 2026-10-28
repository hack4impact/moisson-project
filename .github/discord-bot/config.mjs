import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))

const list = (v) =>
  (v ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)

export const config = {
  discordWebhookUrl: process.env.DISCORD_WEBHOOK_URL || '',
  discordReviewWebhookUrl:
    process.env.DISCORD_REVIEW_WEBHOOK_URL ||
    process.env.DISCORD_WEBHOOK_URL ||
    '',
  githubToken: process.env.GITHUB_TOKEN || '',
  blockedLabels: list(process.env.BLOCKED_LABELS || 'blocked').map((s) =>
    s.toLowerCase(),
  ),
  // Set ALLOW_EVERYONE=false in a busy server: team-wide messages then name each
  // mapped teammate individually instead of firing a real @everyone.
  allowEveryone:
    (process.env.ALLOW_EVERYONE || 'true').toLowerCase() !== 'false',
}

/**
 * GitHub login -> Discord user id.
 *
 * Read from the DISCORD_USER_MAP secret when set, so teammates' Discord ids stay out
 * of a public repo. Falls back to a committed usermap.json for teams that would
 * rather edit a file than a secret.
 */
function loadUserMap() {
  let raw = null

  if (process.env.DISCORD_USER_MAP) {
    try {
      raw = JSON.parse(process.env.DISCORD_USER_MAP)
    } catch (err) {
      console.error(
        '::error::DISCORD_USER_MAP secret is not valid JSON:',
        err.message,
      )
    }
  }

  if (!raw) {
    const file = path.join(here, 'usermap.json')
    try {
      raw = JSON.parse(fs.readFileSync(file, 'utf8'))
    } catch {
      raw = {}
    }
  }

  const map = {}
  for (const [login, id] of Object.entries(raw)) {
    if (login.startsWith('_')) continue // comment keys
    map[login.toLowerCase()] = String(id)
  }
  return map
}

let userMap = null
const getUserMap = () => (userMap ??= loadUserMap())

/**
 * A person, named the way Discord should show them: `@TheirName (their-github-login)`.
 *
 * The Discord mention alone renders as a display name that often looks nothing like
 * the GitHub account it belongs to, which makes "who is that?" a real question on a
 * team message. Carrying the login in brackets answers it without a second lookup.
 *
 * Only a mention that also appears in allowed_mentions actually notifies anyone, so
 * this is safe to use for third parties we merely want to name — they render as a
 * highlighted mention but stay silent unless the caller pings them on purpose.
 *
 * Someone missing from the user map has no Discord name to show, so their GitHub
 * handle stands alone.
 */
export function mention(login) {
  if (!login) return 'someone'
  const id = getUserMap()[String(login).toLowerCase()]
  return id ? `<@${id}> (${login})` : `**@${login}**`
}

/** Discord ids for allowed_mentions — the allowlist of who this message may ping. */
export function mentionIds(logins) {
  const map = getUserMap()
  return [
    ...new Set(
      logins.map((l) => map[String(l ?? '').toLowerCase()]).filter(Boolean),
    ),
  ]
}

/**
 * How to address the whole team in one line.
 *
 * `@everyone` when the server allows it, otherwise every mapped teammate by name —
 * the same intent, delivered the only other way we can deliver it. Returns the
 * logins that must be allowlisted so the fallback actually notifies anyone.
 */
export function everyoneMention() {
  if (config.allowEveryone) return { text: '@everyone', logins: [] }
  const logins = Object.keys(getUserMap())
  return { text: logins.map(mention).join(' ') || 'everyone', logins }
}

export function assertConfig() {
  if (!config.discordWebhookUrl) {
    console.error(
      '::error::DISCORD_WEBHOOK_URL is not set. Add it as a repository secret.',
    )
    process.exit(1)
  }
  const mapped = Object.keys(getUserMap()).length
  console.log(`[config] ${mapped} GitHub login(s) mapped to Discord ids`)
  if (mapped === 0) {
    console.warn(
      '::warning::No user map found — people will be named in messages but not pinged. ' +
        'Set the DISCORD_USER_MAP secret or commit .github/discord-bot/usermap.json.',
    )
  }
}
