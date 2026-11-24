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

## Answers for the SignPath application form

These match the fields on <https://signpath.org/apply>. Check each against the live form and change anything that has stopped being true.

| Field | What to put |
| --- | --- |
| Project Name | `KeyTapLoop` |
| Repository URL | `https://github.com/m-rafiwidya/KeyTapLoop` (the repo must be public) |
| Homepage URL | `https://github.com/m-rafiwidya/KeyTapLoop` |
| Download URL | Optional. Leave it empty until a release exists, then use `https://github.com/m-rafiwidya/KeyTapLoop/releases`. The form says this page must mention the SignPath Foundation, and the README’s “Code signing” section does. |
| Privacy Policy URL | `https://github.com/m-rafiwidya/KeyTapLoop#privacy` (the app collects nothing, but the section is there) |
| Wikipedia URL | Leave empty |
| Tagline | A small Windows utility that repeats or holds a key for a set time and stops the moment you switch windows. |
| Category | The closest of the options. Something like Utilities or Productivity. |
| Description | KeyTapLoop is a small, portable Windows utility that repeats or holds a single key or shortcut for a set number of presses or amount of time. By default it is tied to one chosen window and stops as soon as focus moves to another window. It does nothing until the user starts it, uses the standard Windows SendInput API with normal user rights, needs no installer, makes no network connections and collects no data. It is written in C# with WinForms and is MIT licensed. |
| Reputation | See below. |
| Maintainer Type | Individual |
| Build System | GitHub Actions |
| First name, last name, email | Yours. The email gets the application updates. |
| Company Name | Leave empty unless it applies |
| Primary Discovery Channel | Whichever option fits. If you pick something like “Other” or “Search engine”, a true specific source is Microsoft Learn’s page on code signing options for Windows apps. |
| Checkboxes | Read the Code of Conduct, then tick the required boxes yourself. The communications box is optional. |

### Reputation (be honest here)

The form asks for evidence that the project is widely used. A new project can’t honestly claim that, so don’t. Something like this is true and still useful:

> KeyTapLoop is new, so I don’t have download numbers or press coverage to point to yet. What I can show is that the source is public under the MIT license, every push is built and tested on GitHub Actions (including automated rendering checks of the UI on Windows), and releases will be built by that same workflow. The app makes no network connections and collects no data, which the README documents. I’m applying early so the first public release can be signed from the start.
>
> Build and test history: https://github.com/m-rafiwidya/KeyTapLoop/actions

If they ask for more track record, tagging a first release and letting a few real people use it is the straightforward answer. I couldn’t find any published minimum for stars or downloads.

### Before you tick the Code of Conduct box

The certificate is issued to the SignPath Foundation, which becomes the publisher of your software, so it can’t risk signing anything it considers unwanted. The terms page said it isn’t final yet, and I couldn’t read its full list of what’s not allowed, because that site isn’t reachable from the environment I work in. KeyTapLoop sends simulated keyboard input, which some people associate with game tools. Read the Code of Conduct yourself, and if anything in it looks like it could apply, ask them before applying.

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
