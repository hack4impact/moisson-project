/**
 * Scheduled nudge for work that has gone quiet.
 *
 * Run on a cron by .github/workflows/discord-reminders.yml. Posts one digest that
 * pings each person with their open, assigned, unblocked issues that have seen no
 * recent activity.
 *
 * Deliberately silent about:
 *   - blocked issues        — nagging someone about work they cannot start is noise
 *   - unassigned issues     — there is nobody to remind
 *   - recently touched work — an issue commented on this morning is not stale
 *
 * Silence is the intended output when everyone is current.
 */
import { assertConfig, mention } from './config.mjs'
import { COLORS, clamp, sendDiscord } from './discord.mjs'
import { blockingPhrase, countBlocking, getBlockState } from './blocking.mjs'
import { listOpenIssues } from './github.mjs'

assertConfig()

const repoFullName = process.env.GITHUB_REPOSITORY
if (!repoFullName) {
  console.error('::error::GITHUB_REPOSITORY is not set.')
  process.exit(1)
}
const [owner, repo] = repoFullName.split('/')

const staleDays = Number(process.env.STALE_DAYS || 2)
const now = Date.now()
const daysSince = (iso) => (now - new Date(iso).getTime()) / 86_400_000

const issues = await listOpenIssues(owner, repo)
console.log(`[reminders] ${issues.length} open issues in ${repoFullName}`)

/** login -> issues that person could be working on right now but has not touched. */
const byAssignee = new Map()
let skippedBlocked = 0
let skippedFresh = 0
let skippedUnassigned = 0
let unreadable = 0

for (const issue of issues) {
  const assignees = (issue.assignees ?? []).map((a) => a.login).filter(Boolean)
  if (assignees.length === 0) {
    skippedUnassigned++
    continue
  }

  const idle = daysSince(issue.updated_at)
  if (idle < staleDays) {
    skippedFresh++
    continue
  }

  // One unreachable issue must not sink the whole digest.
  let block
  try {
    block = await getBlockState(owner, repo, issue)
  } catch (err) {
    console.warn(`[reminders] skipping #${issue.number}: ${err.message}`)
    unreadable++
    continue
  }
  if (block.blocked) {
    skippedBlocked++
    continue
  }

  const blocking = (await countBlocking(owner, repo, issue.number)) ?? 0

  for (const login of assignees) {
    if (!byAssignee.has(login)) byAssignee.set(login, [])
    byAssignee.get(login).push({ issue, idle, blocking })
  }
}

console.log(
  `[reminders] ${byAssignee.size} person(s) with stale work; ` +
    `skipped ${skippedUnassigned} unassigned, ${skippedFresh} recently active, ${skippedBlocked} blocked` +
    (unreadable > 0 ? `, ${unreadable} unreadable` : ''),
)

if (byAssignee.size === 0) {
  console.log('[reminders] nothing to nag about — staying quiet')
  process.exit(0)
}

// People holding up the most work lead the digest; idle time breaks ties.
const heldUp = (rows) => rows.reduce((n, r) => n + r.blocking, 0)
const sorted = [...byAssignee.entries()].sort((a, b) => {
  const diff = heldUp(b[1]) - heldUp(a[1])
  if (diff !== 0) return diff
  return (
    Math.max(...b[1].map((x) => x.idle)) - Math.max(...a[1].map((x) => x.idle))
  )
})

const fields = sorted.slice(0, 25).map(([login, rows]) => {
  const held = heldUp(rows)
  return {
    name: `@${login} — ${rows.length} open${held > 0 ? `, ${blockingPhrase(held)}` : ''}`,
    value: clamp(
      rows
        .sort((a, b) => b.blocking - a.blocking || b.idle - a.idle)
        .map(
          ({ issue, idle, blocking }) =>
            `[#${issue.number}](${issue.html_url}) ${String(issue.title).slice(0, 44)}` +
            ` — quiet ${Math.floor(idle)}d${blocking > 0 ? ` · ⛔ ${blocking}` : ''}`,
        )
        .join('\n'),
      1000,
    ),
  }
})

const total = sorted.reduce((n, [, rows]) => n + rows.length, 0)
const totalBlocking = sorted.reduce((n, [, rows]) => n + heldUp(rows), 0)

await sendDiscord({
  content:
    `${sorted.map(([login]) => mention(login)).join(' ')} — reminder: ` +
    `${total} issue${total === 1 ? '' : 's'} still open with no activity in ${staleDays}+ days` +
    (totalBlocking > 0 ? `, together ${blockingPhrase(totalBlocking)}.` : '.'),
  pingLogins: sorted.map(([login]) => login),
  embed: {
    title: 'Open work that has gone quiet',
    url: `https://github.com/${repoFullName}/issues`,
    color: COLORS.review,
    description:
      'Everything here is assigned, unblocked, and workable — it just has not moved. ' +
      'Close it, comment with where it stands, or unassign yourself if it is not yours any more.\n\n' +
      '⛔ marks how many other issues are waiting on that one.',
    fields,
    footer: {
      text:
        skippedBlocked > 0
          ? `${skippedBlocked} blocked issue(s) left out — nothing to do on those yet.`
          : 'Blocked and unassigned issues are left out.',
    },
    timestamp: new Date().toISOString(),
  },
})
