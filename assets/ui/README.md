# Source indicators

The circular Steam icon is taken from the official Steam store favicon:
https://store.steampowered.com/favicon.ico
Retrieved 2026-11-03.

`steam-favicon.ico` is the unmodified source. `steam-48.pbgra` contains its
48 x 48 pixel, 32-bit frame in top-down BGRA order with premultiplied alpha.
This is a pixel-format conversion for Noesis; the artwork is not redrawn.
The runtime embeds only the 9,216-byte pixel data and shares one BitmapSource
across all mod rows. No file or network access is required to show the icon.

Steam and the Steam logo are trademarks and/or registered trademarks of
Valve Corporation in the U.S. and/or other countries.
Branding reference: https://partner.steamgames.com/doc/marketing/branding

The local-folder outline is defined as a simple UI geometry in SourceIcons.cs.
