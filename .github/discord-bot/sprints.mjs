/**
 * Sprint maths, kept free of network and Discord so it can be unit-tested.
 *
 * A "sprint" is a GitHub milestone: it has a title, a due date, and a set of
 * issues. Percentage complete is counted from the issues themselves rather than
 * the milestone's own `open_issues`/`closed_issues` counters, because those
 * counters also include pull requests filed against the milestone.
 */

const DAY = 86_400_000

export function percent(closed, total) {
  if (!total) return 0
  return Math.round((closed / total) * 100)
}

/** `████████░░░░░░░░░░░░` — a bar that survives Discord's proportional font. */
export function progressBar(pct, width = 20) {
  const safe = Math.max(0, Math.min(100, Number(pct) || 0))
  const filled = Math.round((safe / 100) * width)
  return '█'.repeat(filled) + '░'.repeat(width - filled)
}

/**
 * Where a milestone stands relative to its due date.
 * A milestone with no due date is never overdue — there is nothing to be late for.
 */
export function dueState(milestone, now = Date.now()) {
  if (!milestone?.due_on) {
    return { dated: false, overdue: false, days: 0, text: 'no due date set' }
  }

  const remaining = new Date(milestone.due_on).getTime() - now

  if (remaining >= 0) {
    const days = Math.ceil(remaining / DAY)
    return {
      dated: true,
      overdue: false,
      days,
      text: days <= 1 ? 'due within a day' : `due in ${days} days`,
    }
  }

  const days = Math.floor(-remaining / DAY)
  return {
    dated: true,
    overdue: true,
    days,
    text:
      days === 0
        ? 'overdue since earlier today'
        : `overdue by ${days} day${days === 1 ? '' : 's'}`,
  }
}

export const isOverdue = (milestone, now = Date.now()) =>
  dueState(milestone, now).overdue

/**
 * The sprint the team is in right now.
 *
 * The nearest deadline that has not passed yet, because that is what everyone is
 * working towards. If every dated sprint is already past due, the most recent one
 * is still the current sprint — it is late, not finished. Undated milestones are a
 * last resort, newest first, since a team that never sets due dates still wants a
 * number on Wednesday.
 */
export function pickCurrentSprint(milestones, now = Date.now()) {
  const open = (milestones ?? []).filter((m) => m && m.state !== 'closed')

  const dated = open
    .filter((m) => m.due_on)
    .sort((a, b) => new Date(a.due_on) - new Date(b.due_on))

  const upcoming = dated.find((m) => new Date(m.due_on).getTime() >= now)
  if (upcoming) return upcoming
  if (dated.length > 0) return dated[dated.length - 1]

  const undated = open.slice().sort((a, b) => b.number - a.number)
  return undated[0] ?? null
}

/**
 * Break a set of issues down by state and by assignee.
 *
 * An issue with two assignees counts once for each of them, so the per-person
 * numbers can add up to more than the sprint total. That is the honest reading:
 * both people own it.
 */
export function tallyIssues(issues) {
  let closed = 0
  let open = 0
  const byPerson = new Map()
  const unassignedOpen = []

  for (const issue of issues ?? []) {
    const done = issue.state === 'closed'
    if (done) closed++
    else open++

    const logins = (issue.assignees ?? []).map((a) => a.login).filter(Boolean)
    if (logins.length === 0) {
      if (!done) unassignedOpen.push(issue)
      continue
    }

    for (const login of logins) {
      const row = byPerson.get(login) ?? { closed: 0, open: 0, openIssues: [] }
      if (done) row.closed++
      else {
        row.open++
        row.openIssues.push(issue)
      }
      byPerson.set(login, row)
    }
  }

  const total = closed + open
  return {
    closed,
    open,
    total,
    pct: percent(closed, total),
    byPerson,
    unassignedOpen,
  }
}

/** People with unfinished work first; ties broken by who has closed the least. */
export function sortByRemaining(byPerson) {
  return [...byPerson.entries()].sort(
    (a, b) => b[1].open - a[1].open || a[1].closed - b[1].closed,
  )
}
