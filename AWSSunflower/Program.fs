namespace CounterApp

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Threading
open global.Elmish
open Avalonia.FuncUI.Elmish.ElmishHook
open CounterApp.SerialPortModule

module Main =

    let view () =
        Component(fun ctx ->
            let writableModel = ctx.useState (ApiExplorer.init (), true)
            let model, dispatch = ctx.useElmish(writableModel, ApiExplorer.update)

            // Port polling effect
            ctx.useEffect(
                handler = (fun _ ->
                    let polling = startPortPolling (fun ports ->
                        dispatch (ApiExplorer.PortsUpdated ports)
                    )
                    { new IDisposable with member _.Dispose() = polling.Dispose() }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )

            // Polling + loco detection timers
            ctx.useEffect(
                handler = (fun _ ->
                    let timer = DispatcherTimer()
                    timer.Interval <- TimeSpan.FromMilliseconds(200.0)
                    timer.Tick.Add(fun _ ->
                        if writableModel.Current.IsPolling then dispatch ApiExplorer.PollingTick
                    )
                    timer.Start()

                    let locoTimer = DispatcherTimer()
                    locoTimer.Interval <- TimeSpan.FromSeconds(1.0)
                    locoTimer.Tick.Add(fun _ ->
                        if writableModel.Current.ApiConfig.IsSome then dispatch ApiExplorer.DetectLoco
                    )
                    locoTimer.Start()

                    { new IDisposable with
                        member _.Dispose() = timer.Stop(); locoTimer.Stop() }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )

            ApiExplorer.mainView model dispatch
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "AWS Sunflower"
        base.Content <- Main.view ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
