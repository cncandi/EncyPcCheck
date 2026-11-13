# ency-extension-template

Template for an [ENCY](https://encycam.com) extension that **publishes itself to the
[ENCY Extension Store](https://apps.encycam.com) on every version tag**. Write code,
push a tag — the extension appears in the store. No files copied or uploaded by hand.

## Quick start

**No git, no terminal?** Open [apps.encycam.com/publish](https://apps.encycam.com/publish) →
**A folder with the extension**: install the store app on GitHub once, name the extension, pick the
folder with your code (`<Name>.csproj`, `package.info.json`, `<Name>.settings.json`) and press
**Upload and publish**. The store creates the repository from this template, commits the folder,
builds and publishes — the result shows on the same page. An AI assistant can do the same from the
editor: the `ency-extension-store` MCP server's `publish_folder` (this repository registers it for
Claude Code and Cursor; install once with `dotnet tool install -g EncySoftware.ExtensionStoreMcp`).
Everything below is the same pipeline driven by hand.

1. **Use this template** — [this link](https://github.com/new?template_owner=EncySoftware&template_name=ency-extension-template) opens the form with the template already chosen; name
   the repository after your extension, e.g. `PocketMill`. The first push renames the placeholder inside `src/` to match, on its own, so
   `git clone` gets you a project already called by your name.
2. Let this repository publish under that name — once, and **nothing goes into GitHub**: open
   [your profile in the store](https://apps.encycam.com/account) → *Connect*.
   Signing in to the store is the proof it is you; no token is created, so there is no secret to
   store, rotate or leak.

   Every publish after that — the first one included — authenticates with the workflow's own GitHub
   OIDC token, which GitHub issues per run and which expires on its own.
3. Write your code in `src/` (start at `Extension.cs`), fill `src/readme.md` (it becomes the
   store card README) and `description`/`author` in `src/package.info.json`.

   The API itself is documented outside this repo — point your editor's assistant at these too,
   it does not know this API on its own:
   **[reference](https://docs.encycam.com/CAMAPI/3/en/)** (every interface and method) ·
   **[lessons](https://docs.encycam.com/CAMAPI/3/en/src/Lessons/Main.html)** (the same API in order,
   from a first extension) ·
   **[cam-api-examples](https://github.com/EncySoftware/cam-api-examples/tree/v3/main/docs)**
   (a worked example of every extension kind) ·
   **[design rules](https://github.com/EncySoftware/cam-api-examples/blob/v3/main/docs/general/design-rules.md)**
   (how an extension should look and behave inside ENCY: layout, colours, wording, a checklist).
4. Publish — no commands needed: open **Actions → publish-to-ency-store → Run workflow** and press
   the button with the fields empty. It works out the next version, tags the commit, builds, packs
   and publishes; the card link is in the job summary.

   From a terminal instead: `git tag v0.1.0 && git push --tags`.

   The first run may stop once at each one-time step, and the button in its job summary leads
   straight there: **connect the repository** (when the run asks — you skipped step 2), **register
   and accept the terms** (if you are new to the store) and **confirm the declaration** (always, for
   the first release: the template ships an answer — see *What the store asks you to declare*). Do it
   in the browser, then re-run the job.

## Already have a project that was not made from this template?

Keep it where it is. The workflow assumes this template's layout, so bring three things over:

1. The code under `src/`, with `src/<Name>.settings.json` (the manifest — the store refuses a
   package without one) and `src/package.info.json` next to it. No `package.info.json` yet? The
   MCP tool writes one from your csproj (`ency-extension-mcp update-extension`, or any publish).
2. `Directory.Build.props` and `Directory.Build.targets` from the repository root, as they are:
   they give ANY csproj under the repository the flat output and the `PackReady` target the
   workflow builds with — your own `.csproj` needs no edit.
3. `.github/workflows/publish.yml`, copied as is.

Then connect the repository to the extension name — in the browser at
[apps.encycam.com/account](https://apps.encycam.com/account) → *Connect*, or from a terminal with
`ency-extension-mcp claim MyCoolExtension owner/MyCoolExtension` — and **Actions →
publish-to-ency-store → Run workflow**, exactly as above.

## Rather stay in the console?

Optional, and only worth it if you also want your editor's assistant to do this for you. Needs the
.NET 8 SDK:

```bash
dotnet tool install -g EncySoftware.ExtensionStoreMcp
ency-extension-mcp setup
ency-extension-mcp claim MyCoolExtension owner/MyCoolExtension   # same as step 2, from a terminal
```

`setup` signs you in to the store (your licsys account; only a refresh token is kept on your
machine) and, in Cursor or Claude Code, registers the MCP server. Add `--no-login` to skip the
sign-in. With that in place you can skip the steps above and simply ask: *"create an ENCY extension
called MyCoolExtension"*, then *"publish it as 0.1.0"*.

## Local build & try-out

```powershell
dotnet build src -c Release -t:PackReady   # flat, pack-ready output in src\bin\Release
```

(`-t:PackReady` = normal build + sweeps the SDK doc-xmls out of the output; see `Directory.Build.targets`.)

To try the built extension in a local ENCY before publishing: install it via the store card
after a publish (new extensions are reachable by direct link right away), or — if you have the
ENCY pack CLI — pack `src\bin\Release` yourself and use file install. CI needs no packing tool:
the store backend packs the uploaded build output itself.

## What the store asks you to declare

Two things:

- **Developer registration and the Developer Agreement**, before the first thing you publish. Open
  [apps.encycam.com/publish](https://apps.encycam.com/publish), sign in, answer the short registration
  (a company or you personally, free or paid extensions, your details) and press **I Agree**. Until
  you have, every publish route — this workflow included — stops with a link to that page.
- **Reserved functionality**, with every submission. Schedule A of the Publishing Policy lists the
  capabilities ENCY licenses separately; your manifest says which of them your extension provides, in
  `reservedDomains` of `src/package.info.json` (Schedule B §B.3.2). The template ships the answer
  "none":

  ```json
  "reservedDomains": []
  ```

  If the extension does provide some, list each area with the licence Schedule A assigns to it:

  ```json
  "reservedDomains": [
    { "domain": "A-05", "entitlement": "ENCY Nesting", "capabilities": ["nesting.layout"] }
  ]
  ```

  The areas and their licences are in
  [Schedule A](https://encycam.com/legal/extension-store/reserved-functionality/) and at
  `https://apps.encycam.com/api/legal/reserved-functionality`. The first release with an answer stops
  with a link to confirm it in the browser — confirm, then run the job again. Releases with that
  answer then publish at once, until the store asks again: when the answer changes, when a new
  version of Schedule A comes into force, when the words of the statements you confirm change, or
  when a run is credited to someone else: once the repository is bound to the package (its first
  publication binds it), every run from it is credited to whoever bound it, so a colleague's run uses
  that person's confirmation.

## Layout

| Path | Purpose |
|---|---|
| `src/Extension.cs` | your extension logic (`IExtensionUtility.Run`) |
| `src/ExtensionFactory.cs` | entry point ENCY looks for (`CAMAPI.ExtensionFactory`) — keep the class/namespace |
| `src/<YourName>.settings.json` | declares the extensions of this dll for ENCY (ids must match the factory) |
| `src/package.info.json` | store metadata: packageId, version, `category` (what the extension DOES — see the list on the store's publish page), `tags` (keep the `ency-extension` marker!), sdkVersion, `reservedDomains` (*What the store asks you to declare*, above) |
| `src/readme.md` | store card README |
| `src/screenshots/` | PNG/JPG pictures of your extension — they become the card's screenshots, and the first one becomes its cover |
| `.github/workflows/publish.yml` | tag → build → pack → publish |

## Rules worth knowing

- The `ency-extension` tag in `package.info.json` is the **store marker** — remove it and the
  catalog will not index your package (the publish step fails fast on this).
- The tag `vX.Y.Z` overrides the version in `package.info.json` at publish time.
- `sdkVersion` controls the "minimal ENCY version" hint on the card — bump it when you bump
  the `EncySoftware.CAMAPI.Sdk.Net` package.
- The publisher (token owner) becomes the extension owner in the store.
- A manifest without `reservedDomains` is a warning today and a refusal from 1 November 2026.
