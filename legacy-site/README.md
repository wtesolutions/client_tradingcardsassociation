# Legacy static site (holding folder)

This folder is the pre-ASP.NET GitHub Pages site: the HTML pages, CSS/JS, images, and the hobby-shop CSV that were previously at the repository root.

**Why it is here:** the live marketing site on `main` is still a flat GitHub Pages site. The `app/ntca-platform` branch is the start of the ASP.NET Core app that will be hosted on **IIS on Windows Server 2025**. The old pages are kept so we can port content, regenerate `db/seed` files, and open the preview locally. They are **not** the production host going forward.

**Do not merge this branch to `main` until GitHub Pages is turned off** (or the root HTML is restored). Merging would move `index.html` out of the Pages root and take tradingcardsassociation.com offline.

## Opening the pages

Relative links already use `./` (`./site.css`, `./assets/...`, `home.html`, etc.), so they still work when you open files from this folder:

- `legacy-site/index.html` — teaser / invite landing
- `legacy-site/home.html` — inner home
- `legacy-site/shops.html` — hobby shop directory
- `legacy-site/hobby-shop-directory.csv` — Beckett import used by `db/tools/generate_seed`

Repo-level files left at the root: `CNAME`, `.nojekyll`, `.gitignore`, and (on this branch) `README.md`, `docs/`, `db/`.
