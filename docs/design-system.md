# Tidverk UI system

Tidverk uses [accntech/shad-ui](https://github.com/accntech/shad-ui) as its Avalonia UI foundation. The package is pinned centrally so upgrades are explicit and reproducible.

## Application setup

`App.axaml` loads the upstream theme before Tidverk-specific styles:

```xml
<Application.Styles>
  <shadui:ShadTheme />
  <StyleInclude Source="avares://Tidverk/Styles/TidverkStyles.axaml" />
</Application.Styles>
```

`MainWindow` derives from `ShadUI.Window`. The application uses ShadUI's `Sidebar`, `SidebarItem`, `Card`, `Badge`, button classes, `ControlAssist.Label`, icon geometries, typography classes, and semantic theme resources directly.

The workspace and Settings each own a ShadUI sidebar. Both bind the same expansion state to real `ToggleButton` controls and use the component's documented `Expanded` and `MinWidth` behavior. Settings maps its three sections to selected sidebar items and keeps Back at the top.

The shell follows Avalonia's documented navigation pattern for a small fixed page set: `TransitioningContentControl` renders typed page records through data templates. Each page is a `UserControl`; the shell keeps persistent navigation and transient overlays outside page content.

## Ownership boundary

ShadUI owns generic component appearance and behavior. Tidverk does not copy or wrap those controls.

Tidverk owns only product-specific presentation:

- Month ledger and calendar layouts
- Workday status pills and warning notices
- Day-editor sheet geometry
- Modal sizing for Tidverk workflows
- The weekend background token

Those rules live in `src/Tidverk.App/Styles/TidverkStyles.axaml` and `AppTokens.axaml`. They use ShadUI semantic resources instead of hard-coded light or dark palettes.

## Icon contract

- Use only `ShadUI.Icons` geometries in product UI.
- Render interface icons in a 16 by 16 optical box with the shared `ui-icon` class.
- Put leading button icons in `ButtonAssist.Icon` and use an 8-pixel content gap.
- Put navigation icons in `SidebarItem.Icon`; the sidebar owns the 12-pixel icon-to-label gap.
- Use ShadUI's `Icon` button class for square icon-only actions, put the icon in `ButtonAssist.Icon`, and keep an automation name and tooltip. Tidverk's shared `Button.Icon` rule resets inherited variant padding so mixed classes remain centered.

## Conventions

1. Use an existing ShadUI control or style class before adding a Tidverk selector.
2. Use `DynamicResource` for theme-sensitive color, radius, border, and surface values.
3. Keep business state and commands in `MainWindowViewModel`; visual controls do not duplicate state.
4. Preserve keyboard commands, focus targets, automation names, and readable disabled states.
5. Add or refresh headless snapshots when a visible workflow changes.
6. Give pages navigation or main landmarks, headings semantic levels, icon-only actions automation names, and status messages live-region metadata.

## Visual verification

Render every major state without opening a desktop window:

```bash
TIDVERK_SNAPSHOT_DIR="$PWD/artifacts/ui-snapshots" \
  dotnet test tests/Tidverk.App.Tests/Tidverk.App.Tests.csproj -c Release \
  --filter FullyQualifiedName~Ui_surfaces_render_to_headless_snapshots_when_requested
```

The suite captures ledger, calendar, editor, setup, settings, catch-up, report, and dark ledger states.

## Upstream references

- [ShadUI repository](https://github.com/accntech/shad-ui)
- [ShadUI NuGet package](https://www.nuget.org/packages/ShadUI)
- [shadcn/ui](https://ui.shadcn.com/)
- [Avalonia navigation](https://docs.avaloniaui.net/docs/how-to/navigation-how-to)
- [Avalonia resources](https://docs.avaloniaui.net/docs/app-development/resources)
- [Avalonia accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)
- [Avalonia styles](https://docs.avaloniaui.net/docs/styling/styles)
- [Avalonia layout](https://docs.avaloniaui.net/docs/layout)
