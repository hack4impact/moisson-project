/**
 * Sprint reporting. Two modes, both driven by cron from a workflow.
 *
 *   MODE=progress   Wednesday and Saturday. Tells the whole team, with an @everyone,
 *                   how far through the current sprint they are.
 *   MODE=overdue    Every second day. Names everyone still holding an open issue in
 *                   a sprint whose due date has passed, and escalates to @everyone
 *                   when overdue work has nobody assigned at all — an unassigned
 *                   late task is the team's problem, not one person's.
 *
 * A "sprint" is a GitHub milestone. Percentages come from the issues in it, not the
 * milestone's own counters, which also count pull requests.
 *
 * progress speaks on schedule, because a status update people expect is not noise.
 * overdue stays silent when nothing is late.
 */
import { assertConfig, everyoneMention, mention } from './config.mjs'
import { COLORS, clamp, sendDiscord } from './discord.mjs'
import { listMilestoneIssues, listMilestones } from './github.mjs'
import {
  dueState,
  isOverdue,
  percent,
  pickCurrentSprint,
  progressBar,
  sortByRemaining,
  tallyIssues,
} from './sprints.mjs'

assertConfig()

const repoFullName = process.env.GITHUB_REPOSITORY
if (!repoFullName) {
  console.error('::error::GITHUB_REPOSITORY is not set.')
  process.exit(1)
}
const [owner, repo] = repoFullName.split('/')

const mode = (process.env.MODE || 'progress').toLowerCase()
const now = Date.now()

/** Up to `max` issue links, with an honest "+N more" rather than a silent cut. */
function issueList(issues, max = 5) {
  const shown = issues
    .slice(0, max)
    .map((i) => `[#${i.number}](${i.html_url}) ${String(i.title).slice(0, 44)}`)
  if (issues.length > max) shown.push(`…and ${issues.length - max} more`)
  return clamp(shown.join('\n'), 1000)
}

const capitalise = (text) => text.charAt(0).toUpperCase() + text.slice(1)

/** Wednesday / Saturday: where the current sprint stands, to the whole team. */
async function reportProgress(milestones) {
  const sprint = pickCurrentSprint(milestones, now)
  const issues = await listMilestoneIssues(owner, repo, sprint.number, 'all')
  const { pct, closed, open, total, byPerson, unassignedOpen } =
    tallyIssues(issues)
  const due = dueState(sprint, now)

  console.log(
    `[sprint] ${sprint.title}: ${closed}/${total} closed (${pct}%), ${due.text}`,
  )

  if (total === 0) {
    console.log('[sprint] current sprint has no issues yet — staying quiet')
    return
  }

  const everyone = everyoneMention()
  const headline =
    pct === 100
      ? `**${sprint.title}** is done — all ${total} issues closed.`
      : `**${sprint.title}** is **${pct}% complete** — ${closed}/${total} issues closed, ${open} still open.`

  const fields = sortByRemaining(byPerson)
    .slice(0, 24)
    .map(([login, row]) => {
      const owned = row.closed + row.open
      return {
        name: `@${login} — ${row.closed}/${owned} done (${percent(row.closed, owned)}%)`,
        value: row.open === 0 ? '✅ nothing left' : issueList(row.openIssues),
      }
    })

  if (unassignedOpen.length > 0) {
    fields.push({
      name: `Unassigned — ${unassignedOpen.length} open`,
      value: `Nobody has picked these up yet:\n${issueList(unassignedOpen)}`,
    })
  }

  await sendDiscord({
    content: `${everyone.text} — sprint check-in: ${headline} ${capitalise(due.text)}.`,
    everyone: true,
    pingLogins: everyone.logins,
    embed: {
      title: `Sprint progress — ${sprint.title}`,
      url: sprint.html_url,
      color: due.overdue
        ? COLORS.review
        : pct === 100
          ? COLORS.unblocked
          : COLORS.ready,
      description:
        `\`${progressBar(pct)}\` **${pct}%**\n\n` +
        `**${closed}** closed · **${open}** open · ${due.text}` +
        (sprint.description ? `\n\n${clamp(sprint.description, 200)}` : ''),
      fields,
      footer: { text: 'Sprint check-in — every Wednesday and Saturday.' },
      timestamp: new Date().toISOString(),
    },
  })
}

/** Every second day: who is late, and what nobody has claimed. */
async function reportOverdue(milestones) {
  const late = milestones.filter((m) => isOverdue(m, now))
  if (late.length === 0) {
    console.log('[sprint] no sprint is past its due date — staying quiet')
    return
  }

  const openIssues = []
  const finished = []
  for (const milestone of late) {
    const issues = await listMilestoneIssues(
      owner,
      repo,
      milestone.number,
      'open',
    )
    // A late sprint with nothing open is finished; somebody just never closed it.
    if (issues.length === 0) finished.push(milestone)
    else openIssues.push(...issues)
  }

  const { byPerson, unassignedOpen, open } = tallyIssues(openIssues)
  const people = sortByRemaining(byPerson)

  console.log(
    `[sprint] ${late.length} overdue sprint(s), ${open} open issue(s), ` +
      `${people.length} person(s) behind, ${unassignedOpen.length} unassigned`,
  )

  if (open === 0) {
    console.log(
      '[sprint] overdue sprints have no open issues left — staying quiet',
    )
    return
  }

  const everyone = everyoneMention()
  const needsEveryone = unassignedOpen.length > 0

  const pings = [
    ...(needsEveryone ? [everyone.text] : []),
    ...people.map(([login]) => mention(login)),
  ].join(' ')

  const worst = late
    .slice()
    .sort((a, b) => new Date(a.due_on) - new Date(b.due_on))[0]
  const worstDue = dueState(worst, now)

  const fields = people
    .slice(0, needsEveryone ? 24 : 25)
    .map(([login, row]) => ({
      name: `@${login} — ${row.open} still open`,
      value: issueList(row.openIssues),
    }))

  if (needsEveryone) {
    fields.push({
      name: `⚠️ Unassigned — ${unassignedOpen.length} overdue with no owner`,
      value: `These are late and nobody is on them. Someone has to take them:\n${issueList(unassignedOpen)}`,
    })
  }

  await sendDiscord({
    content:
      `${pings} — ⏰ **${worst.title}** is ${worstDue.text}` +
      (late.length > 1
        ? ` (and ${late.length - 1} other sprint past due)`
        : '') +
      ` with **${open}** issue${open === 1 ? '' : 's'} still open.` +
      (needsEveryone
        ? ` ${unassignedOpen.length} of them ${unassignedOpen.length === 1 ? 'has' : 'have'} nobody assigned.`
        : ''),
    everyone: needsEveryone,
    pingLogins: [
      ...people.map(([login]) => login),
      ...(needsEveryone ? everyone.logins : []),
    ],
    embed: {
      title:
        late.length === 1
          ? `Overdue sprint — ${worst.title}`
          : `${late.length} overdue sprints`,
      url: worst.html_url,
      color: COLORS.review,
      description:
        late
          .map((m) => `• **${m.title}** — ${dueState(m, now).text}`)
          .join('\n') +
        '\n\nClose what is done, move what is not into the next sprint, or say where it stands. ' +
        'A sprint that quietly runs past its date stops meaning anything.',
      fields,
      footer: {
        text: finished.length
          ? `${finished.map((m) => m.title).join(', ')}: every issue closed — the milestone itself is still open.`
          : 'Overdue check runs every second day.',
      },
      timestamp: new Date().toISOString(),
    },
  })
}

const milestones = await listMilestones(owner, repo, 'open')
console.log(
  `[sprint:${mode}] ${milestones.length} open milestone(s) in ${repoFullName}`,
)

if (milestones.length === 0) {
  console.log('[sprint] no open milestones — nothing to report on')
  process.exit(0)
}

if (mode === 'progress') {
  await reportProgress(milestones)
} else if (mode === 'overdue') {
  await reportOverdue(milestones)
} else {
  console.error(
    `::error::Unknown MODE "${mode}" — expected "progress" or "overdue".`,
  )
  process.exit(1)
}
