import { COLORS, clamp, sendDiscord } from '../discord.mjs'
import { mention } from '../config.mjs'

function prEmbed(pr, repository, color, extraFields = []) {
  return {
    title: `#${pr.number} ${clamp(pr.title, 200)}`,
    url: pr.html_url,
    color,
    author: pr.user
      ? { name: pr.user.login, icon_url: pr.user.avatar_url }
      : undefined,
    fields: [
      {
        name: 'Branch',
        value: `\`${pr.head?.ref}\` → \`${pr.base?.ref}\``,
        inline: true,
      },
      { name: 'Repo', value: repository.full_name, inline: true },
      ...extraFields,
    ],
    timestamp: new Date().toISOString(),
  }
}

const size = (pr) =>
  `+${pr.additions ?? '?'} / -${pr.deletions ?? '?'} across ${pr.changed_files ?? '?'} files`

/** Issues this PR will close when it lands, as written in the description. */
const closesIssues = (pr) =>
  (pr.body ?? '').match(/(?:close[sd]?|fixe?[sd]?|resolve[sd]?)\s+#(\d+)/gi) ??
  []

/**
 * A PR has just appeared and wants eyes on it.
 *
 * Drafts are skipped on purpose — a draft is the author saying "not yet". They are
 * announced later instead, when the author marks the PR ready for review.
 *
 * Reviewers set at creation time get a real ping. A PR opened with nobody on it is
 * still announced, because a PR no one knows about is exactly the one that rots, but
 * it pings nobody — there is no one to ping yet.
 */
async function onOpened(payload) {
  const { pull_request: pr, repository } = payload
  if (pr.draft) {
    console.log(
      `[notify] #${pr.number} is a draft — staying quiet until it is ready`,
    )
    return
  }

  const author = pr.user?.login
  const reviewers = (pr.requested_reviewers ?? [])
    .map((r) => r.login)
    .filter(Boolean)
  const teams = (pr.requested_teams ?? []).map((t) => t.name).filter(Boolean)
  const closes = closesIssues(pr)

  const asked = [...reviewers.map(mention), ...teams.map((t) => `**@${t}**`)]
  const opened =
    payload.action === 'ready_for_review' ? 'is ready for review' : 'is open'

  await sendDiscord({
    review: true,
    content: asked.length
      ? `${asked.join(' ')} — PR #${pr.number} from ${mention(author)} ${opened} and needs your review.`
      : `PR #${pr.number} from ${mention(author)} ${opened} — no reviewer assigned yet.`,
    pingLogins: reviewers,
    embed: prEmbed(
      pr,
      repository,
      asked.length ? COLORS.review : COLORS.ready,
      [
        { name: 'Size', value: size(pr), inline: true },
        {
          name: 'Reviewers',
          value: asked.length ? asked.join(', ') : '_nobody yet_',
          inline: true,
        },
        ...(closes.length
          ? [{ name: 'Closes', value: closes.join(', ') }]
          : []),
      ],
    ),
  })
}

/** Someone was put on the hook for a review. */
async function onReviewRequested(payload) {
  const { pull_request: pr, repository } = payload
  const reviewer = payload.requested_reviewer?.login
  const team = payload.requested_team?.name
  const author = pr.user?.login ?? 'someone'

  await sendDiscord({
    review: true,
    content: `${reviewer ? mention(reviewer) : `**@${team}**`} — code review requested by ${mention(author)} on #${pr.number}.`,
    pingLogins: reviewer ? [reviewer] : [],
    embed: prEmbed(pr, repository, COLORS.review, [
      { name: 'Size', value: size(pr), inline: true },
      {
        name: 'Reviewer',
        value: reviewer ? mention(reviewer) : `team @${team}`,
        inline: true,
      },
    ]),
  })
}

/** Merged, not merely closed — an abandoned PR is not worth a celebration. */
async function onClosed(payload) {
  const { pull_request: pr, repository } = payload
  if (!pr.merged) return

  const author = pr.user?.login
  const merger = payload.sender?.login
  const closes = closesIssues(pr)

  await sendDiscord({
    content: `${mention(author)} — your PR #${pr.number} was merged into \`${pr.base?.ref}\` by ${mention(merger)}. 🎉`,
    pingLogins: [...new Set([author, merger].filter(Boolean))],
    embed: prEmbed(pr, repository, COLORS.merged, [
      ...(closes.length ? [{ name: 'Closes', value: closes.join(', ') }] : []),
      { name: 'Merged', value: size(pr), inline: true },
    ]),
  })
}

export async function handlePullRequest(payload) {
  switch (payload.action) {
    case 'opened':
    case 'ready_for_review':
      return onOpened(payload)
    case 'review_requested':
      return onReviewRequested(payload)
    case 'closed':
      return onClosed(payload)
    default:
      return
  }
}

/** A submitted review is news for the PR author, not the reviewer. */
export async function handlePullRequestReview(payload) {
  if (payload.action !== 'submitted') return

  const { review, pull_request: pr, repository } = payload
  const author = pr.user?.login
  const reviewer = review.user?.login

  const line = {
    approved: {
      text: `${mention(author)} — ${mention(reviewer)} approved #${pr.number}. Good to merge.`,
      color: COLORS.unblocked,
    },
    changes_requested: {
      text: `${mention(author)} — ${mention(reviewer)} requested changes on #${pr.number}.`,
      color: COLORS.review,
    },
  }[String(review.state ?? '').toLowerCase()]

  if (!line) return // plain comments would just be noise

  await sendDiscord({
    review: true,
    content: line.text,
    // The reviewer is pinged as well as the author: they asked for a change and will
    // want the thread in their notifications when the revision lands.
    pingLogins: [...new Set([author, reviewer].filter(Boolean))],
    embed: prEmbed(pr, repository, line.color, [
      {
        name: 'Review',
        value: clamp(review.body, 500) || '_No comment left._',
      },
    ]),
  })
}
