# Required checks — merge and deploy policy

Repository policy: **no merge to `main` and no GitHub Pages publish until
`tools/run-harness.sh` passes.**

## CI enforcement (automatic)

1. **`Regression tests`** runs on every pull request and on every push to
   `main` or `cursor/**`.
2. **`Deploy Web Version to GitHub Pages`** runs only after a **successful**
   `Regression tests` run for a `main` branch push (`workflow_run` gate).
3. **`Deploy Branch Preview to GitHub Pages`** runs only after a **successful**
   `Regression tests` run for a `cursor/**` branch push.

Manual `workflow_dispatch` on deploy workflows still runs the harness in the
build job before publishing (safety net for operators).

## Merge enforcement (one-time repo setting)

GitHub cannot require status checks from workflow files alone — enable branch
protection on `main`:

1. **Settings → Branches → Add branch protection rule** (or edit rule for
   `main`).
2. Enable **Require status checks to pass before merging**.
3. Enable **Require branches to be up to date before merging** (recommended).
4. Search and select status check: **`Regression harness`**  
   (job name from workflow **Regression tests**).
5. Save.

After this, merges stay blocked until the PR’s latest commit has a green
**Regression harness** check.

Owners can still bypass if **Do not allow bypassing** is off; leave bypass
disabled for production stability.

## What contributors should see

| Event | Expected checks |
|-------|-----------------|
| Open / update PR | **Regression harness** green |
| Merge PR to `main` | Regression on `main` push → then live Pages deploy |
| Push to `cursor/…` branch | Regression → branch preview deploy (only if green) |

Local parity before pushing:

```bash
tools/run-harness.sh
```