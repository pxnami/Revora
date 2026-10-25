# Revora landing page

Static HTML, CSS, and JavaScript. No install or build step is required. Open `index.html` directly, or serve this directory with any static web server.

From the repository root:

```powershell
python -m http.server 4173 --directory site
```

Visit `http://localhost:4173`. Deploy the contents of `site/` to any static host. All asset paths are relative so the page also works under a repository subpath.

The interface images are existing Revora UI-test screenshots with synthetic device data. Manrope is self-hosted under the SIL Open Font License; see `assets/FONT-LICENSE.txt`. Update screenshot copies when the desktop interface changes. Download links intentionally lead to GitHub Releases so prerelease status and available assets remain visible.
