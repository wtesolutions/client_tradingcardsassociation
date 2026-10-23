# NTCA Website Style Guide

Version 1.0 · October 23, 2026

## 1. Design direction

Create a premium discovery platform for the National Trading Card Association that combines the authority of a national organization with the energy of collecting. The selected desktop and mobile mockup is the visual reference.

Use a substantial dark navy header and footer, an image-led navy hero, gold primary actions, and bright white discovery sections. The interface should feel established, useful, and welcoming to sports-card and TCG collectors.

The color values, fonts, dimensions, and responsive rules below are recommended implementation specifications inferred from the mockup, not extracted source styles. The logo must come from the original NTCA artwork.

## 2. Brand and messaging

**Primary headline:** Protecting, Promoting and Powering the Hobby.

**Eyebrow:** Built by collectors, for collectors.

**Supporting copy:** A national community of collectors, shops and events uniting the trading card hobby.

**Organization name:** National Trading Card Association

**Primary discovery action:** Explore

**Membership action:** Join the association

Keep copy direct, confident, and community-oriented. Explain practical benefits. Avoid financial speculation, exaggerated authority, and unsupported verification claims.

### Logo treatment

- Use the original NTCA emblem in SVG or a high-resolution transparent PNG. Do not recreate it from the generated mockup.
- Preserve its navy, red, gold, and white details, proportions, and lettering.
- Desktop target: 88–96px emblem height within a 120–128px header.
- Mobile target: 56–64px emblem height within an 88–96px website header, excluding device status UI.
- Keep at least 16px vertical clearance on desktop and 12px on mobile.
- Never overlap the hero, crop the emblem, stretch it, or shrink it to make navigation fit.
- Position the full organization name beside the emblem in white. Keep the name as real text where appropriate; the emblem remains an image.
- Use a static emblem by default rather than continuous spinning animation.

## 3. Color system

| Token | Value | Application |
| --- | --- | --- |
| Navy | `#10182B` | Header, footer, mobile navigation |
| Hero navy | `#172237` | Hero backing and photo overlay |
| Primary ink | `#11192D` | Headings and body emphasis |
| Gold | `#E5B956` | Primary actions and active accents |
| Gold hover | `#D6A744` | Primary button hover |
| Gold light | `#F1D38B` | Optional subtle button highlight |
| Red | `#C92332` | Event date headers and brand details |
| White | `#FFFFFF` | Content surfaces and text on navy |
| Canvas | `#F7F8FA` | Secondary page backgrounds |
| Muted text | `#536077` | Supporting information on white |
| Border | `#DCE1E8` | Cards, inputs, separators |
| Filter surface | `#F0F3F7` | Filter controls |
| Badge surface | `#EAF2FF` | Owner-claimed badges |
| Badge text | `#164C96` | Badge labels |

Use navy text on gold buttons. Use white or light neutral text on navy. Reserve gold for emphasis rather than long text passages on white. Use red sparingly. Validate final color pairings for accessible contrast.

## 4. Typography

Recommended implementation: **Inter** for headings, body text, and controls, with `Arial, sans-serif` fallbacks. A condensed family such as **Barlow Condensed** may be used for the organization-name lockup only. These are recommendations, not identified fonts from the image.

| Element | Desktop | Mobile | Weight / line height |
| --- | --- | --- | --- |
| Hero H1 | 48–60px | 32–38px | 750–800 / 1.05–1.12 |
| Section H2 | 30–36px | 24–28px | 700–800 / 1.15 |
| Card heading | 18–20px | 18px | 700 / 1.25 |
| Body | 16–18px | 16px | 400 / 1.5 |
| Navigation and buttons | 14–16px | 16px | 600 / 1.25 |
| Metadata | 13–14px | 13–14px | 400–500 / 1.4 |
| Eyebrow | 11–12px | 10–11px | 600 / 1.4 |

Use slight negative tracking on large headings and approximately `0.16em` tracking on the uppercase eyebrow. Do not use tightly tracked uppercase text for ordinary navigation or body copy.

Suggested desktop headline wrapping:

> Protecting, Promoting  
> and Powering  
> the Hobby.

Allow natural wrapping at smaller widths. Do not enforce desktop line breaks on mobile.

## 5. Layout and spacing

- Center content in a container with a recommended maximum width of 1440px.
- Use desktop side gutters of 32–48px and mobile gutters of 16–20px.
- Base spacing scale: 4, 8, 12, 16, 24, 32, 48, 64px.
- Use 24px desktop grid gaps and 16px mobile gaps.
- Typical section padding: 40–56px desktop and 28–36px mobile.
- Use restrained 6–8px radii on cards, inputs, and buttons.
- Use thin borders and minimal shadows. Do not place every section inside a floating card.
- Keep content height flexible for long names, text zoom, and translated labels.

The outer frame, phone hardware, status indicators, and presentation shadows in the mockup are presentation elements, not website components.

## 6. Header and navigation

Use a full-width solid navy header. Align the logo lockup left, navigation centrally or to the right, and the gold membership action at the far right. Vertically center all elements.

**Navigation status:** The live URL redirected to an invitation-only teaser during review. Its complete navigation could not be verified. The labels shown in the mockup are provisional:

- Find shops
- Shows & events
- Card history
- About NTCA
- Join the association

Before implementation, obtain the actual navigation labels, destinations, and dropdown hierarchy. Preserve those destinations rather than treating the mockup labels as the approved information architecture.

If the confirmed navigation is longer, use a second navigation row beneath the logo/membership row or an earlier mobile-menu breakpoint. Do not reduce the logo below its intended size. Distinguish utility links from primary navigation only after the real menu is known.

On mobile, show the emblem, organization name, and a clearly labeled menu button. The menu panel must support keyboard operation, Escape dismissal, focus restoration, and a visible close control.

## 7. Hero

Use authentic imagery of collectors browsing trading cards in a hobby shop. The photograph should communicate the hobby immediately through display cases, cards, and people.

- Desktop: text on the left; the main photographic subject on the right.
- Add a navy overlay behind text that fades toward the photo; preserve readable contrast across responsive crops.
- Keep eyebrow, headline, supporting paragraph, and search in a clear hierarchy.
- Set supporting copy to approximately 45–55 characters per line.
- Mobile: retain the headline and search prominently. Use a stronger overlay as needed.
- Let the section grow to fit text and controls; do not use a fixed height that clips content.

## 8. Search component

Preserve the search box from the selected design as a central functional feature.

### Desktop

Place a white search container near the bottom of the hero, with 8px padding, an 8px radius, and three aligned controls:

1. Search type: **Find shops & events**.
2. Location: **City or ZIP code** with a location icon.
3. Primary action: gold **Explore** button.

Recommended control height: 48–52px. Give the two fields most of the width; the action should remain comfortably readable.

### Mobile

Stack the search type, location, and Explore button. Make each full width with 8px gaps. Keep field text at least 16px.

### Behavior

- Provide persistent accessible labels; placeholders alone are insufficient.
- Allow Enter to submit.
- If supporting device location, offer an explicit “Use my location” action.
- Retain search criteria on results pages and when returning from a detail page.
- Support loading, no-results, invalid-location, and service-error states.
- No-results copy should offer a wider radius or fewer filters.

## 9. Categories and filters

Place category navigation immediately below the hero on white, separated by a fine border.

Mockup categories: All cards, Sports cards, Pokémon, Magic: The Gathering, Other TCGs.

Use navy labels, a gold underline for the active category, and simple consistent icons. On mobile, allow horizontal scrolling without wrapping into multiple dense rows. Keep accessible text labels alongside icons.

Shop filters begin with Open now, Distance, and Specialties. Use soft neutral surfaces and compact icons. Provide active states, a clear-all action, and a result count on search-results pages.

Event search should use event-specific controls such as date, location, and event type. Keep shop and event filters distinct.

## 10. Shop cards

Use image-forward cards on white with a subtle border and 6–8px corners.

Recommended content order:

1. Store photograph, approximately 16:9.
2. Shop name.
3. Specialties.
4. City and state.
5. Owner-claimed status, only when supported by data.
6. View shop link.

Position the save button at the top-right of the photograph with a readable backing. Use a real button with a descriptive accessible label and an explicit saved state.

Use consistent image heights and `object-fit: cover`. Do not truncate essential identifying information. Keep card navigation and the save action as separate interactive elements without nested links/buttons.

“Owner claimed” means the owner has claimed the listing; it does not imply independent verification, quality certification, or NTCA endorsement. Show ratings, opening hours, and distances only when reliable data is available.

## 11. Events

Section heading: **Meet. Trade. Collect.**

Supporting copy: **Find upcoming shows and community events near you.**

Use compact horizontal cards with a red month block, large navy day number, title, date/time, venue/location, optional thumbnail, and View event link.

On narrow screens, stack cards and prioritize date, name, and venue. Include the year and timezone where needed. Generate date labels from actual event data so weekdays, dates, and years agree. The mockup dates and venue details are illustrative and must not be copied into production.

## 12. History feature

Eyebrow: **150 years of the hobby**

Title: **From Advertising Cards to a Global Hobby**

Action: **Explore the history**

Use archival imagery with warm cream or paper tones. On wide screens, the feature may occupy a right-hand column beside the shop/event area. On mobile, place it after discovery content without hiding it entirely.

Use an explicit editorial-card component so it is not mistaken for another shop listing.

## 13. Footer and mobile navigation

Desktop footer: solid navy, intact emblem and association name, concise navigation, and a gold-outline membership action. Provide more vertical space if additional real navigation or legal links are required. Do not compress text to match the mockup height.

Mobile: navy bottom navigation with Shops, Events, and Saved as provisional quick actions. Use gold for the selected icon and label and white/light neutral for inactive items. The bottom bar supplements rather than replaces the full menu and footer.

If fixed, reserve content space beneath it and account for `env(safe-area-inset-bottom)` so it never obscures controls or the final content row.

## 14. Responsive rules

| Width | Recommended layout |
| --- | --- |
| Below 768px | Mobile header, stacked search, single-column cards, horizontal categories |
| 768–1199px | Two-column content where space allows; compact or collapsed navigation |
| 1200px and above | Full header navigation; shop/event area with optional history column |

Choose the exact navigation breakpoint based on the confirmed menu width. Check 320px, 375px, 768px, 1024px, and 1440px widths, plus 200% text zoom. The page must not develop horizontal overflow outside intentional category scrolling.

## 15. Interaction and accessibility

- Aim for at least 44 × 44px touch targets.
- Provide clearly visible keyboard focus on light and dark surfaces.
- Use semantic header, navigation, main, section, and footer elements.
- Use one H1, then logical H2 and H3 headings.
- Distinguish links, buttons, filters, and informational badges visually and semantically.
- Do not rely on color alone for active, saved, error, or status states.
- Provide meaningful image alternatives; decorative imagery may use empty alt text.
- Keep body text contrast at least 4.5:1 and large text at least 3:1 as implementation acceptance targets.
- Keep animation short and subtle and respect reduced-motion preferences.
- Use skeletons or reserved card dimensions to avoid layout shifts during loading.

## 16. Starter CSS tokens

```css
:root {
  --ntca-navy: #10182b;
  --ntca-hero: #172237;
  --ntca-ink: #11192d;
  --ntca-gold: #e5b956;
  --ntca-gold-hover: #d6a744;
  --ntca-red: #c92332;
  --ntca-white: #ffffff;
  --ntca-canvas: #f7f8fa;
  --ntca-muted: #536077;
  --ntca-border: #dce1e8;
  --ntca-filter: #f0f3f7;
  --ntca-radius: 8px;
  --ntca-container: 1440px;
  --ntca-font: "Inter", Arial, sans-serif;
}

.ntca-container {
  width: min(calc(100% - 40px), var(--ntca-container));
  margin-inline: auto;
}

.ntca-header,
.ntca-footer {
  color: var(--ntca-white);
  background: var(--ntca-navy);
}

.ntca-hero h1 {
  font-size: clamp(2rem, 4vw, 3.75rem);
  line-height: 1.08;
  letter-spacing: -0.035em;
  font-weight: 800;
}

.ntca-primary-action {
  background: var(--ntca-gold);
  color: var(--ntca-ink);
  min-height: 48px;
  border-radius: var(--ntca-radius);
  font-weight: 600;
}

.ntca-primary-action:hover {
  background: var(--ntca-gold-hover);
}

:focus-visible {
  outline: 3px solid #2463c5;
  outline-offset: 3px;
}

.ntca-header :focus-visible,
.ntca-footer :focus-visible {
  outline-color: var(--ntca-gold);
}
```

## 17. Implementation acceptance checklist

- [ ] Original logo is used and fully contained within the taller header.
- [ ] Desktop and mobile use the exact approved headline.
- [ ] Search layout and prominence match the selected concept.
- [ ] Real live-site navigation has been supplied and mapped before launch.
- [ ] Header/footer remain navy; main discovery content remains light.
- [ ] Cards, photos, spacing, and typography are consistent.
- [ ] Mockup shop names, events, dates, addresses, and badges are replaced with real data.
- [ ] Search, filters, save controls, and mobile menu have complete interaction states.
- [ ] Layout works with keyboard navigation, text zoom, and narrow screens.
- [ ] Image dimensions are reserved; below-fold imagery is lazy-loaded.
- [ ] Hero imagery is optimized and its text contrast is checked at each crop.

## 18. Developer handoff prompt

Implement the NTCA homepage from the selected visual concept and this guide. Preserve the substantial navy header with the original emblem, navy footer, gold actions, bright content surfaces, and image-led collector aesthetic. Use the headline “Protecting, Promoting and Powering the Hobby.” Preserve the search component as a primary feature. Build responsive reusable components for navigation, hero/search, category navigation, filters, shop cards, event cards, and the history feature. Treat the confirmed live-site navigation as authoritative once supplied; current mockup labels remain provisional. Use real text and production assets rather than embedding the mockup as a webpage image. Keep sample listings clearly separated from production data.
