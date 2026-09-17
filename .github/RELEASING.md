# Release operations

## Branches and versions

Pushes to develop run the release coordinator and reusable CI. Public changes produce alpha.N versions; main produces stable 0.x versions. Tags are created automatically after validation. Do not create tags to start a release.

fix and perf increment patch; feat and explicit breaking changes increment minor before 1.0. Documentation, CI and test-only changes do not publish. Dependency-update commits use fix(deps). The four kernel packages share a version; Reed versions independently.

Use Conventional Commit PR titles for squash merges. Merge promotion and synchronization PRs with a merge commit to preserve release ancestry. The automatic promotion PR requires human merge; dependency PRs enable auto-merge after required checks. Stable Reed refuses a prerelease SDK.

## One-time setup

1. Deploy the workflow changes to develop in both repositories while RELEASE_ENABLED is absent or false. Reed's feed-access workflow must exist on its default branch before the first kernel release.
2. Create a GitHub App installed only on PSLoom/PSLoom and PSLoom/Reed. Grant repository Contents and Pull requests read/write, Actions read/write (dispatch and observe Reed's feed probe), and Workflows write (promotion/synchronization may carry workflow changes). It needs no administration bypass or package publishing permission. Tokens request only the permissions needed by their job.
3. Set repository variable RELEASE_APP_ID and secret RELEASE_APP_PRIVATE_KEY in both repositories. Never commit the private key. Tokens are minted per job and revoked by the action.
4. Enable repository auto-merge. Protect develop and main with the three OS build checks, benchmark-report check and Conventional PR title check. Inspect the actual check names from a completed PR run before configuring required contexts. Permit dependency PR auto-merge without bypass; use review policy for promotion PRs. Disallow force pushes/deletion on release branches and release tags. Automation branches need force-with-lease updates.
5. Publish the initial kernel packages, then grant Reed Actions read access in each package's settings: PSLoom, PSLoom.Warp, PSLoom.Build, PSLoom.TestKit. Configure inherited access if appropriate. The kernel stays in draft until a workflow in Reed downloads all four with Reed's own GITHUB_TOKEN. If initial access setup misses the probe's ten-minute window, fix settings and resume.
6. Set RELEASE_ENABLED=true in PSLoom only after its dry run and integration checks pass. Activate Reed after its pinned SDK is readable and CI passes. No workflow enables this variable automatically.

The first main promotion must contain the product and workflows; an initial empty main is not a release candidate. The first stable SDK must exist before Reed promotion is prepared.

## Dry run

From the repository directory:

    gh workflow run release.yml --ref develop -f dry_run=true

A dry run builds and validates but creates no tag, release, package or PR. With RELEASE_ENABLED unset, ordinary branch pushes behave the same way. CI remains active for PRs and can also be dispatched separately.

PowerShell is installed at 7.6.4 using the public NuGet tool package; the .NET SDK follows global.json. CI logs both versions. Startup and benchmark durations are informational during baseline collection. Missing/invalid benchmark measurements and allocation violations still fail.

## Kernel and consumer integration

Kernel CI builds temporary SDK packages and runs Reed at the immutable reedSha in .github/release.json. Update that SHA through a reviewed kernel PR when validating a coordinated adaptation. The consumer uses an isolated package cache and local feed; no private feed credential is passed to its code.

Normal Reed CI uses the pinned PSLoomVersion from GitHub Packages. A 403 is an access failure, not evidence that the version is missing. The temporary integration test complements, rather than replaces, the real feed check.

## Publication and recovery

The coordinator serializes both channels within a repository. It rechecks branch HEAD before creating a tag, so an obsolete validated commit cannot claim a new version. Intermediate pushes may be coalesced.

Final packages are built once and tested on all three operating systems. The module ZIP is derived from the package. A draft release preserves assets and release-manifest.json before any feed upload. The manifest binds the commit, version, dependency and checksums. Publication never overwrites an asset or package.

To resume a partially published release:

    gh workflow run release.yml --ref develop -f dry_run=false -f resume_tag=psloom/v0.1.0-alpha.1

Use reed/v... for Reed and --ref main for stable releases. Recovery downloads the preserved draft assets, checks the tag/commit and manifest, verifies already published packages and sends only missing packages. Existing package contents must match every ZIP entry; timestamps/compression differences alone are tolerated. Missing or changed content stops recovery. If artifacts were lost before the draft was populated, investigate using the retained Actions artifact; recovery never silently rebuilds the same version.

Reed access is checked before completing the kernel release. The kernel feed job and consumer-access job are separate: a failed access probe leaves uploaded packages and draft assets intact. Configure access, then rerun failed jobs or use recovery. RELEASE_ENABLED must still be true.

For a failed post-release PR update, rerun only the independent workflow:

    gh workflow run release-prs.yml --ref develop -f version=0.1.0-alpha.1 -f channel=develop

The workflow first verifies the release is complete. It reuses one automation branch per purpose/channel, rejects dependency downgrades and stops on human edits or conflicts. A failed consumer PR does not invalidate the completed kernel release.

Promotion of Reed uses a preparation branch with the stable SDK matching develop's SDK base. An unavailable stable SDK blocks preparation. main-to-develop synchronization uses a separate PR and preserves a newer SDK already in develop; conflicts require review.

## Local verification

    pwsh -NoProfile -File .github/scripts/Test-Release.ps1
    pwsh -NoProfile -File .github/scripts/Test-ReleaseHistory.ps1
    pwsh -NoProfile -File .github/scripts/Test-Publication.ps1
    pwsh -NoProfile -File .github/scripts/Test-ReleasePullRequests.ps1
    pwsh -NoProfile -File .github/scripts/Test-BudgetReporting.ps1

These cover semantic version selection, real Git histories and publication recovery using local service adapters. They do not publish externally. Kernel package checks additionally use build/Test-Packages.ps1 and build/Test-BuildSdk.ps1. Cross-platform Actions and real GitHub App/feed checks are still required before activation.
