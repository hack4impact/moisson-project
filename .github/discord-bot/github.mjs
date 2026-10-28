import { config } from './config.mjs'

// Actions sets GITHUB_API_URL itself; honouring it also lets the tests point the
// whole client at a local fixture server instead of the network.
const API = process.env.GITHUB_API_URL || 'https://api.github.com'

/** Raised when a request could not be completed, as opposed to legitimately empty. */
export class GitHubUnavailable extends Error {
  constructor(path, status) {
    super(`GitHub returned ${status} for ${path} after retries`)
    this.name = 'GitHubUnavailable'
    this.status = status
  }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

/**
 * Minimal GitHub client over fetch — deliberately dependency-free so the workflow
 * needs no `npm install` step and cannot break on a transitive update.
 *
 * Distinguishes "this does not exist" (null) from "I could not find out"
 * (throws GitHubUnavailable). The dependency endpoints return intermittent 504s,
 * and treating one as "no dependencies" would put a false statement in a message.
 */
async function gh(path, { attempts = 3 } = {}) {
  let lastStatus = 0

  for (let attempt = 1; attempt <= attempts; attempt++) {
    let res
    try {
      res = await fetch(`${API}${path}`, {
        headers: {
          accept: 'application/vnd.github+json',
          'x-github-api-version': '2022-11-28',
          'user-agent': 'moisson-discord-bot',
          ...(config.githubToken
            ? { authorization: `Bearer ${config.githubToken}` }
            : {}),
        },
      })
    } catch (err) {
      // Network-level failure: worth retrying.
      lastStatus = 0
      if (attempt < attempts) {
        await sleep(500 * attempt)
        continue
      }
      throw new GitHubUnavailable(path, `network error (${err.message})`)
    }

    // Genuinely absent, or an endpoint this repo does not have. Not a failure.
    if (res.status === 404 || res.status === 410) return null

    // Rate limited or a secondary limit — back off and retry.
    if (
      res.status === 429 ||
      (res.status === 403 && res.headers.get('x-ratelimit-remaining') === '0')
    ) {
      lastStatus = res.status
      const reset = Number(res.headers.get('x-ratelimit-reset') || 0) * 1000
      const wait =
        reset > Date.now()
          ? Math.min(reset - Date.now(), 15_000)
          : 1000 * attempt
      if (attempt < attempts) {
        console.warn(
          `[github] rate limited on ${path}, waiting ${Math.round(wait / 1000)}s`,
        )
        await sleep(wait)
        continue
      }
      throw new GitHubUnavailable(path, res.status)
    }

    // Permission denied with quota left is a real answer: we cannot see this.
    if (res.status === 403) return null

    // 5xx — GitHub had a bad moment. Retry before believing it.
    if (res.status >= 500) {
      lastStatus = res.status
      if (attempt < attempts) {
        console.warn(
          `[github] ${res.status} on ${path}, retry ${attempt}/${attempts - 1}`,
        )
        await sleep(600 * attempt)
        continue
      }
      throw new GitHubUnavailable(path, res.status)
    }

    if (!res.ok) throw new GitHubUnavailable(path, res.status)

    return res.json()
  }

  throw new GitHubUnavailable(path, lastStatus)
}

export async function getIssue(owner, repo, number) {
  return gh(`/repos/${owner}/${repo}/issues/${number}`)
}

/** Issues that must close before this one can start (GitHub's native dependencies). */
export async function nativeBlockedBy(owner, repo, number) {
  const data = await gh(
    `/repos/${owner}/${repo}/issues/${number}/dependencies/blocked_by?per_page=100`,
  )
  return Array.isArray(data) ? data : []
}

/** The inverse: issues waiting on this one. */
export async function nativeBlocking(owner, repo, number) {
  const data = await gh(
    `/repos/${owner}/${repo}/issues/${number}/dependencies/blocking?per_page=100`,
  )
  return Array.isArray(data) ? data : []
}

/** Open issues whose body names this one as a blocker in plain text. */
export async function searchTextDependents(owner, repo, number) {
  const found = new Map()
  for (const phrase of ['blocked by', 'depends on']) {
    const q = encodeURIComponent(
      `repo:${owner}/${repo} is:issue is:open in:body "${phrase} #${number}"`,
    )
    const data = await gh(`/search/issues?q=${q}&per_page=50`)
    for (const item of data?.items ?? []) {
      if (item.pull_request) continue
      found.set(item.number, item)
    }
  }
  return [...found.values()]
}

/**
 * Paginated issue listing. Pull requests are filtered out — GitHub's issues
 * endpoint returns them too, and a PR is never a task on a sprint board.
 */
async function listIssues(owner, repo, query) {
  const out = []
  for (let page = 1; page <= 10; page++) {
    const data = await gh(
      `/repos/${owner}/${repo}/issues?per_page=100&page=${page}&${query}`,
    )
    if (!Array.isArray(data) || data.length === 0) break
    out.push(...data.filter((i) => !i.pull_request))
    if (data.length < 100) break
  }
  return out
}

export async function listOpenIssues(owner, repo) {
  return listIssues(owner, repo, 'state=open')
}

/** Every issue filed against one milestone — closed ones included, for the ratio. */
export async function listMilestoneIssues(
  owner,
  repo,
  milestone,
  state = 'all',
) {
  return listIssues(owner, repo, `state=${state}&milestone=${milestone}`)
}

/** Sprints. Sorted by due date so the nearest deadline comes first. */
export async function listMilestones(owner, repo, state = 'open') {
  const data = await gh(
    `/repos/${owner}/${repo}/milestones?state=${state}&per_page=100&sort=due_on&direction=asc`,
  )
  return Array.isArray(data) ? data : []
}
