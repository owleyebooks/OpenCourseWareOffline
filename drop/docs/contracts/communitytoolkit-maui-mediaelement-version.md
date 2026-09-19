# CommunityToolkit.Maui.MediaElement — verified package version

**Verified live, round v36, see AUDIT_TRAIL v36.** Not re-derived from
memory or prior-round prose — checked directly against nuget.org and
github.com this round.

## nuget.org (authoritative for `dotnet add package`)

- Latest stable: **10.0.0**, published 2026-06-02.
- Targets `net10.0`; compatible with `net10.0-android36.0`,
  `net10.0-ios26.0`, `net10.0-maccatalyst26.0`, `net10.0-windows10.0.19041`.
- Depends on `Microsoft.Maui.Controls >= 10.0.60`; on Android additionally
  depends on `Xamarin.AndroidX.Media3.*` (>= 1.8.0).
- Source: https://www.nuget.org/packages/CommunityToolkit.Maui.MediaElement

## This project's own pin

`OcwOffline/OcwOffline.csproj` already has:
```
<PackageReference Include="CommunityToolkit.Maui.MediaElement" Version="10.0.0" />
```
This exactly matches nuget.org's current latest. No csproj change needed.

## Resolving the v28 "10.0.0 vs 14.0.0" discrepancy note

The `CommunityToolkit/Maui` GitHub repo is a monorepo covering several
sub-packages (`CommunityToolkit.Maui` core, `.MediaElement`, `.Camera`,
etc.), each with its **own independent version number** — they are not
one shared version line. The core `CommunityToolkit.Maui` package is
currently at 14.1.1 on GitHub's release list; `.MediaElement` is a
separate, lower-numbered line, currently 10.0.0 per nuget.org. The v28
note comparing "nuget shows 10.0.0" against "GitHub shows up to 14.0.0"
was almost certainly reading the core package's release number against
the MediaElement package's nuget number — not an actual version skew on
the same package.

One loose end, logged rather than papered over: GitHub's own
`.MediaElement`-tagged releases, as fetched this round (page 1 of a
paginated list, sorted by date), topped out at `9.0.0-mediaelement`
(06 Apr 2026) — one release behind nuget.org's `10.0.0` (02 Jun 2026).
Most plausible explanation is a pagination/listing artifact rather than
a real gap, since nuget.org is the authoritative source for what
`dotnet add package` actually resolves — but that specific gap wasn't
independently chased down further this round (would need paging deeper
into GitHub's release list), so it's not claimed as fully explained,
just not blocking.

## Bottom line for the backlog

The discrepancy this item was tracking is resolved: the project's pin
is current and correct. No build-attempt or human check is or was
actually required to answer this — it just needed the two live pages
compared side by side, which this round did.
