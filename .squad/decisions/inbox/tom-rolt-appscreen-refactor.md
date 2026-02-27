# Decision: ApplicationScreen Refactor — Rename + Component Split

**Date:** 2026-02-28  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/application-screen-refactor

## Context

The `ApiExplorer` naming no longer reflects the app's purpose — it's the full application screen, not just an API explorer. The monolithic `ApiExplorerViews.fs` (530 lines) contained 7 view functions plus AppColors, making navigation and maintenance difficult.

## Decision

1. **Rename** all `ApiExplorer*` modules to `ApplicationScreen*` (ApplicationScreen, ApplicationScreenHelpers, ApplicationScreenCommands, ApplicationScreenUpdate)
2. **Move** all files into `AWSSunflower/ApplicationScreen/` folder
3. **Split** ApiExplorerViews.fs into 7 single-responsibility component files: ConnectionPanel, StatusBar, TreeBrowser, EndpointViewer, BindingsPanel, SerialPortPanel, MainView
4. **Extract** inner rendering helpers (`renderEndpoint`, `renderBinding`, `serialStatus`) to flatten deep nesting
5. **Promote** AppColors from `module private` to public `module AppColors` in Helpers.fs

## Key Design Choices

- **Namespace preserved:** All files still use `namespace CounterApp` — no namespace rename (separate concern)
- **Component functions are public:** Removed `private` from all view functions since they're now cross-file
- **MainView is pure composition:** Only calls other component functions, no logic
- **BindingsPanel dependency:** Only component needing `ApplicationScreenCommands` (for `currentSubscription.Value` read)

## Outcome

✅ Build succeeds, all 200 tests pass. Pure rename + restructure with zero behavior changes.
