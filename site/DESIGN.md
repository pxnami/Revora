# Landing page design

The page extends Revora's blue identity and existing logo into a warm, restrained editorial surface. Its purpose is to explain the Windows utility and lead visitors to the prerelease with its limitations visible.

## Tokens

- Paper: `#f7f7f0`; foreground: `#202b30`; secondary text: `#555f63`.
- Action and emphasis: `#244cba`; rules: `#d6d9d2`; screenshot stage: `#dfe7f5`.
- Display: self-hosted Manrope, weight 600, tracking -0.035em (hero -0.04em).
- Body: Segoe UI, 16px, line height 1.6.
- Content: maximum 1240px; gutters 56px desktop, 32px tablet, 20px mobile.
- Controls: 6px corners; 56px primary action height; visible blue keyboard focus.

## Composition and behavior

Large blue headline, product explanation and download action, then an actual app screenshot stage. Three native buttons switch the screenshot and expose selection through `aria-pressed`; the caption announces the change. Feature rows avoid a repeated card grid. The blue restore section retains the data-loss and validation limitations. Native disclosure elements provide keyboard-operable FAQs. Reduced-motion preferences disable the single logo rotation and smooth scrolling.

Use real app images and label synthetic test data. Do not imply validated device restores, guaranteed data preservation, or Activation Lock bypass. Keep the desktop application independent of the site.
