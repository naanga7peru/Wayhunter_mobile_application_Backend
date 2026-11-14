# WAYHUNTER - M7-MS09-T02 Git Workflow

## Task Information

- Owner: M7 - Thashvini
- Task ID: M7-MS09-T02
- Type: Operations / Documentation
- Branch: task/m7-ms09-t02-git-workflow
- Target branch: develop
- Status: Draft - Pending Team Review
- Dependency: M1-MS01 acceptance not yet verified

## Purpose

Define the proposed Git workflow, task branch naming,
module ownership, review responsibility and merge checks
for all seven WAYHUNTER members.

## Module Ownership

| Member | Module |
|---|---|
| M1 | Identity, Profile, Friends, Settings |
| M2 | Traveler Planning, Maps, AI |
| M3 | Active Trips, Safety, Notifications |
| M4 | Hunter, Places |
| M5 | Games, Anti-Cheat, Leaderboards |
| M6 | Coins, Wallet, Vouchers |
| M7 | Home, Advertisements, Campaigns, Analytics |

## Branch Strategy

- main: Stable release branch
- develop: Reviewed integration branch
- task/: Individual task development branches

Required branch naming:

task/mX-msYY-tZZ-short-description

Example:

task/m7-ms09-t02-git-workflow

## Required Task Workflow

1. Read the assigned Second Brain task.
2. Verify dependencies and acceptance criteria.
3. Update local develop from origin.
4. Create one new branch for the task.
5. Change only task-related files.
6. Run relevant build, tests and checks.
7. Commit and push the task branch.
8. Open a Pull Request targeting develop.
9. Request an authorized peer review.
10. Verify required checks and approval.
11. Merge the PR into develop.
12. Update task evidence and status.
13. Start the next task from latest develop.

## Pull Request Requirements

Every PR must document:

- Task ID
- Owner
- Files changed
- Implementation summary
- Build/test or documentation evidence
- Acceptance criteria
- Dependencies
- Known blockers
- Reviewer

## Merge Rules

- No direct pushes to main or develop.
- No merging without required review approval.
- Broken or incomplete code must not be merged.
- Draft PRs may be used for early feedback.
- Do not reuse a completed task branch.
- Do not start the next task before the previous
  task PR has been merged.
- Shared contracts require affected owner review.
- Milestone acceptance requires all required
  tasks and acceptance evidence.

## Development Order

Backend
-> Flutter Frontend
-> Final API Integration
-> Angular Admin

## Validation / Evidence

- Previous inventory PR #8 merged into develop.
- New task branch created from updated develop.
- Branch naming follows the task ID.
- No application source changes required.

## Acceptance Checklist

- [x] Module ownership documented
- [x] Task branch naming documented
- [x] PR workflow documented
- [x] Merge checks documented
- [ ] Team leader confirms reviewer assignments
- [ ] Required branch protections verified
- [ ] Authorized member reviews this document
- [ ] PR merged into develop
- [ ] Dependency and task acceptance confirmed

## Notes

This document records the proposed team process.
It does not claim that GitHub branch protection
settings or reviewer assignments are configured.
Those items require separate verification.
