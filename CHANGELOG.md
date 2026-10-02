# Changelog

## 0.1.2

- Remove text-button padding from the fixed-width settings button so its gear is centred and unclipped.
- Check the actual host button style, glyph width and horizontal/vertical centring in native validation.
- Use the runtime top-bar adapter in previews instead of overriding padding by hand.

## 0.1.1

- Keep plugin-managed top-bar tabs on the same live theme style as built-in tabs.
- Apply the style to tabs already present and tabs added later, including after a theme change.
- Align the settings button and standardise tab fonts, height and spacing.
- Restore adapter-owned changes and detach event handlers when the plugin stops.
- Add native regression checks for stale tab styles, selection, theme changes and cleanup.

## 0.1.0

- Initial smoked and frosted glass themes with matching terminal palettes.
