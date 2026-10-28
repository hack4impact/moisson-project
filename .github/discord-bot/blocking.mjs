import { config } from './config.mjs'
import {
  getIssue,
  nativeBlockedBy,
  nativeBlocking,
  searchTextDependents,
} from './github.mjs'

// "Blocked by", "blocked-by:", "depends on", "waiting on", "blocker:"
const KEYWORD =
  /(?:blocked[\s_-]*by|depends?[\s_-]*(?:up)?on|waiting[\s_-]*on|blockers?)\s*:?/gi

// "#12", "owner/repo#12", or a full issue URL, anchored at the current position.
const REF =
  /^(?:(?:([\w.-]+)\/([\w.-]+))?#(\d+)|https?:\/\/github\.com\/([\w.-]+)\/([\w.-]+)\/(?:issues|pull)\/(\d+))/i

// Whitespace, commas, "and", and list/checkbox markers between consecutive refs.
const SEPARATOR = /^(?:[\s,;&]+|and\b|[-*+]|\[[ xX]?\])+/i

/**
 * Pull blocker references out of an issue body.
 * Only refs following a blocking keyword count, so a passing "see also #99"
 * elsewhere in the description is not mistaken for a dependency.
 */
export function parseBlockerRefs(body, defaults) {
  const text = String(body ?? '')
  const out = new Map()
  KEYWORD.lastIndex = 0

  let keyword
  while ((keyword = KEYWORD.exec(text)) !== null) {
    let rest = text.slice(keyword.index + keyword[0].length)

    for (;;) {
      const gap = rest.match(SEPARATOR)
      if (gap) rest = rest.slice(gap[0].length)

      const ref = rest.match(REF)
      if (!ref) break

      const owner = ref[1] ?? ref[4] ?? defaults.owner
      const repo = ref[2] ?? ref[5] ?? defaults.repo
      const number = Number(ref[3] ?? ref[6])

      const self =
        number === defaults.number &&
        owner.toLowerCase() === defaults.owner.toLowerCase() &&
        repo.toLowerCase() === defaults.repo.toLowerCase()
      if (!self)
        out.set(`${owner}/${repo}#${number}`.toLowerCase(), {
          owner,
          repo,
          number,
        })

      rest = rest.slice(ref[0].length)
      if (!SEPARATOR.test(rest)) break
    }
  }
  return [...out.values()]
}

export function hasBlockedLabel(issue) {
  const labels = (issue?.labels ?? []).map((l) =>
    String(l.name ?? l).toLowerCase(),
  )
  return labels.some((name) => config.blockedLabels.includes(name))
}

/**
 * Everything standing between an issue and someone starting work on it:
 * native GitHub dependencies, "blocked by #N" text, and blocking labels.
 */
export async function getBlockState(owner, repo, issue) {
  const number = issue.number
  const blockers = new Map()

  for (const dep of await nativeBlockedBy(owner, repo, number)) {
    const key =
      `${dep.repository?.full_name ?? `${owner}/${repo}`}#${dep.number}`.toLowerCase()
    blockers.set(key, {
      number: dep.number,
      title: dep.title,
      state: dep.state,
      html_url: dep.html_url,
    })
  }

  for (const ref of parseBlockerRefs(issue.body, { owner, repo, number })) {
    const key = `${ref.owner}/${ref.repo}#${ref.number}`.toLowerCase()
    if (blockers.has(key)) continue
    const target = await getIssue(ref.owner, ref.repo, ref.number)
    if (!target) continue // deleted or unreadable — do not block on a ghost
    blockers.set(key, {
      number: target.number,
      title: target.title,
      state: target.state,
      html_url: target.html_url,
    })
  }

  const all = [...blockers.values()]
  const open = all.filter((b) => b.state === 'open')
  const labelBlocked = hasBlockedLabel(issue)

  return {
    blockers: all,
    openBlockers: open,
    labelBlocked,
    blocked: open.length > 0 || labelBlocked,
  }
}

/** Issues that were waiting on this one — natively linked and text-linked. */
export async function findDependents(owner, repo, number) {
  const found = new Map()

  for (const dep of await nativeBlocking(owner, repo, number)) {
    const full = dep.repository?.full_name ?? `${owner}/${repo}`
    const [o, r] = full.split('/')
    found.set(`${full}#${dep.number}`.toLowerCase(), {
      owner: o,
      repo: r,
      number: dep.number,
    })
  }

  for (const item of await searchTextDependents(owner, repo, number)) {
    found.set(`${owner}/${repo}#${item.number}`.toLowerCase(), {
      owner,
      repo,
      number: item.number,
    })
  }

  return [...found.values()]
}

/**
 * How many still-open issues are waiting on this one.
 *
 * Uses only the native dependency links — the text search behind findDependents costs
 * scarce search-API quota, which is too expensive to spend per issue in a digest.
 */
export async function countBlocking(owner, repo, number) {
  try {
    const deps = await nativeBlocking(owner, repo, number)
    return deps.filter((d) => d.state === 'open').length
  } catch (err) {
    // Unknown is not zero. Callers omit the claim rather than assert "blocks nothing".
    console.warn(
      `[blocking] could not count dependents of #${number}: ${err.message}`,
    )
    return null
  }
}

/** "blocking 3 other issues", or "" for zero and for unknown. */
export function blockingPhrase(count) {
  if (!count) return ''
  return `blocking ${count} other issue${count === 1 ? '' : 's'}`
}

/** Embed field value for a blocking count, honest about a failed lookup. */
export function blockingFieldValue(count) {
  if (count === null) return 'could not check'
  return count > 0
    ? `⛔ ${count} other open issue${count === 1 ? '' : 's'}`
    : 'nothing'
}

export function formatBlockerList(blockers) {
  if (blockers.length === 0) return 'none'
  return blockers
    .map(
      (b) =>
        `[#${b.number}](${b.html_url}) ${String(b.title ?? '').slice(0, 60)}`,
    )
    .join('\n')
}
