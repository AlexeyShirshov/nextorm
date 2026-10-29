# Promo assets

Static promotional images for NextORM. Both are authored as SVG and exported to PNG; edit the SVG,
then re-render the PNG — never edit the PNG by hand.

| File | Size | Used for |
|---|---|---|
| `nextorm-social-preview.svg` / `.png` | 1280 × 640 | GitHub repository social preview (Settings → Social preview), README banners, link previews |
| `nextorm-cover-ru.svg` / `.png` | 1200 × 630 | Russian-language cover: Habr post, Telegram, VK |
| `nextorm-square.svg` / `.png` | 1080 × 1080 | Square social card (Telegram, LinkedIn, Instagram) |
| `nextorm-icon.svg` / `.png` | 128 × 128 | NuGet package icon (`PackageIcon` in `src/Directory.Build.props`) |

The three banners share the same composition and palette; only the format, language and the
headline differ. Benchmark numbers appear in all of them — update them together.

## Palette

Taken from the docs diagrams (`docs/assets/diagrams/`): background `#0b1220`, panels `#0e1729` /
`#111d33`, borders `#2b3a55`, text `#f1f5f9` / `#cbd5e1` / `#94a3b8`, accents indigo `#818cf8`,
violet `#c084fc`, cyan `#22d3ee`, orange `#fb923c`.

## Regenerating the PNGs

Requires `rsvg-convert` (librsvg). Run from this directory:

```bash
rsvg-convert -w 1280 -h 640   nextorm-social-preview.svg -o nextorm-social-preview.png
rsvg-convert -w 1200 -h 630   nextorm-cover-ru.svg       -o nextorm-cover-ru.png
rsvg-convert -w 1080 -h 1080  nextorm-square.svg         -o nextorm-square.png
rsvg-convert -w 128  -h 128   nextorm-icon.svg           -o nextorm-icon.png
```

## Notes

- Text uses the font stack `Inter, Segoe UI, Ubuntu, DejaVu Sans` (and `JetBrains Mono, Consolas,
  Ubuntu Mono` for code). The glyphs are baked into the PNG, so a missing font only affects a
  re-render, not the shipped image.
- Benchmark numbers on the banner come from the table in the root `readme.md`
  (`Benchmarks` section). Update the SVG whenever that table changes.
- The icon must stay legible at 32 × 32 px: keep the mark large and avoid fine detail.
