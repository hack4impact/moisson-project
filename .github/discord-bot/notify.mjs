/**
 * Entry point for the GitHub Actions workflow.
 *
 * Actions hands us the event name in GITHUB_EVENT_NAME and the full webhook payload
 * as a file at GITHUB_EVENT_PATH — the same payload a webhook server would receive,
 * minus the transport, the signature check, and the hosting.
 */
import fs from 'node:fs'
import { assertConfig } from './config.mjs'
import { handleIssues } from './handlers/issues.mjs'
import {
  handlePullRequest,
  handlePullRequestReview,
} from './handlers/pull_request.mjs'

assertConfig()

const eventName = process.env.GITHUB_EVENT_NAME
const eventPath = process.env.GITHUB_EVENT_PATH

if (!eventName || !eventPath) {
  console.error('::error::GITHUB_EVENT_NAME / GITHUB_EVENT_PATH are missing.')
  process.exit(1)
}

const payload = JSON.parse(fs.readFileSync(eventPath, 'utf8'))
console.log(
  `[notify] ${eventName}.${payload.action ?? '-'} on ${payload.repository?.full_name}`,
)

try {
  switch (eventName) {
    case 'issues':
      await handleIssues(payload)
      break
    case 'pull_request':
    case 'pull_request_target':
      await handlePullRequest(payload)
      break
    case 'pull_request_review':
      await handlePullRequestReview(payload)
      break
    default:
      console.log(`[notify] nothing to do for ${eventName}`)
  }
} catch (err) {
  // A failed notification must never turn the repo's checks red.
  console.error(`::warning::notification failed: ${err.stack || err.message}`)
}
