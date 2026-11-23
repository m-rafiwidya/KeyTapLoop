# Code signing

Signing `KeyTapLoop.exe` puts a verified publisher name on the file and lets Windows build a reputation for it over time. It makes the download more trustworthy. It does **not** remove the Windows SmartScreen warning on day one.

## What signing does and doesn’t do

- **Does:** shows a real publisher instead of “Unknown publisher”, proves the file wasn’t changed after signing, and lets reputation accumulate against one certificate across releases.
- **Doesn’t:** clear SmartScreen immediately. Microsoft says [extended validation certificates no longer get instant reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options), so a new app starts at zero whichever certificate it uses.
- **Keep one identity.** Reputation attaches to the certificate. Use the same signing identity for every release and always timestamp.

## Recommended route: SignPath Foundation (free for open source)

[SignPath Foundation](https://about.signpath.io/product/open-source) signs qualifying open-source projects for free. It matches this project, but it has requirements:

| Requirement | Status |
| --- | --- |
| OSI-approved open-source license | Met (MIT) |
| **Public** source repository | **Not yet.** The repository is private until the Windows checks pass. |
| Multi-factor authentication on GitHub and SignPath for every team member | Yours to turn on |
| A published code signing policy | Draft below |
| An attribution line in the README once approved | Add after approval |
| Signing driven from GitHub Actions | Ready: see `.github/workflows/release.yml` |

The certificate is issued to SignPath Foundation, so **SignPath Foundation is shown as the publisher**, not you personally. Check the current terms on their site before applying, because requirements can change.

### Steps

1. Finish the Windows checks in [TESTING.md](TESTING.md), then make the repository public.
2. Apply at <https://signpath.org> for an open-source project, using this repository.
3. After approval, in the SignPath dashboard create a project with slug `keytaploop`, a signing policy with slug `release-signing`, and an artifact configuration that signs `KeyTapLoop.exe`. If you pick different slugs, change them in `release.yml`.
4. In the GitHub repository settings, add the secret `SIGNPATH_API_TOKEN` and the variable `SIGNPATH_ORGANIZATION_ID`.
5. Add the attribution line to the README: *Free code signing provided by SignPath.io, certificate by SignPath Foundation.*
6. Tag a release (`v0.1.0`). The workflow signs the exe, then packages and hashes the signed file.

The signing step is switched off until `SIGNPATH_ORGANIZATION_ID` exists, so releases keep working unsigned in the meantime. I couldn’t test the SignPath step from here. Expect to adjust the artifact configuration on first use.

## Answers for the SignPath application

Use these when you fill in the form. Check each against the form’s current fields, and correct anything that isn’t true by then.

| Field | Answer |
| --- | --- |
| Project name | KeyTapLoop |
| Repository | `https://github.com/m-rafiwidya/KeyTapLoop` (must be public when you apply) |
| License | MIT (OSI-approved) |
| One-line description | A free, portable Windows utility that repeats or holds one key or shortcut for a set count or time, with global shortcuts and a run that ends when focus moves. |
| What would be signed | `KeyTapLoop.exe`, a self-contained .NET 10 WinForms executable, built from source by GitHub Actions on tagged releases. |
| Build system | GitHub Actions (`.github/workflows/release.yml`) |
| Network access or telemetry | None. No accounts, no network calls, no telemetry. |
| Does it need elevated rights? | No. It runs with normal user rights. An optional “restart as administrator” button exists for sending keys to administrator windows. |
| Download page | The repository’s Releases page, with a SHA-256 hash for each file |

Be upfront that the app **sends simulated keyboard input**. Automation tools can look like unwanted software to scanners and reviewers, so say clearly what it does, that it is open source, and that it never runs without the user starting it.

## Draft code signing policy

Publish this in the repository (for example in the README) when you apply.

> **Code signing policy**
>
> Free code signing provided by SignPath.io, certificate by SignPath Foundation.
>
> - **What is signed:** `KeyTapLoop.exe`, built from this repository’s source by the GitHub Actions workflow in `.github/workflows/release.yml`. Nothing is signed from a developer’s own machine.
> - **Roles:** the repository owner is author, reviewer and approver of release signing requests.
> - **Privacy:** KeyTapLoop makes no network connections, has no accounts and sends no telemetry. It stores its settings in `%LOCALAPPDATA%\KeyTapLoop\settings.json`.
> - **Verification:** every release publishes a SHA-256 hash next to the download.

## Other routes

- **Azure Artifact Signing** (formerly Trusted Signing): Microsoft’s managed signing service, billed monthly. I couldn’t confirm current pricing or whether individual developers in your country can sign up, so check [Microsoft’s pricing page](https://azure.microsoft.com/pricing/details/artifact-signing/) and the eligibility rules.
- **A commercial certificate** from a certificate authority: costs money every year and usually involves hardware or a cloud signing service. Reputation still has to be earned.
- **Self-signed:** protects nothing for other people’s machines. Don’t use it for releases.
