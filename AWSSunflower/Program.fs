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
open System.Threading.Tasks
open CounterApp.PortDetection
open CounterApp.ApiExplorerUpdate
open CounterApp.ApiExplorerViews

module ErrorHandling =

    let showErrorDialog (message: string) =
        try
            Dispatcher.UIThread.Post(fun () ->
                try
                    let window = Window()
                    window.Title <- "AWS Sunflower — Error"
                    window.Width <- 420.0
                    window.Height <- 180.0
                    window.WindowStartupLocation <- WindowStartupLocation.CenterScreen
                    let panel = StackPanel()
                    panel.Margin <- Thickness(20.0)
                    panel.VerticalAlignment <- Avalonia.Layout.VerticalAlignment.Center
                    let text = TextBlock()
                    text.Text <- message
                    text.TextWrapping <- Avalonia.Media.TextWrapping.Wrap
                    text.Margin <- Thickness(0.0, 0.0, 0.0, 16.0)
                    panel.Children.Add(text)
                    let btn = Button()
                    btn.Content <- "OK"
                    btn.HorizontalAlignment <- Avalonia.Layout.HorizontalAlignment.Center
                    btn.Click.Add(fun _ -> window.Close())
                    panel.Children.Add(btn)
                    window.Content <- panel
                    window.Show()
                with _ -> ()
            )
        with _ -> ()

    let setupGlobalExceptionHandlers () =
#if DEBUG
        () // Debug: let exceptions propagate with full stack traces
#else
        AppDomain.CurrentDomain.UnhandledException.Add(fun args ->
            let ex = args.ExceptionObject :?> Exception
            eprintfn "[UNHANDLED EXCEPTION] %A" ex
            showErrorDialog "An unexpected error occurred. The application will now close."
        )

        TaskScheduler.UnobservedTaskException.Add(fun args ->
            eprintfn "[UNOBSERVED TASK EXCEPTION] %A" args.Exception
            args.SetObserved()
            showErrorDialog "An unexpected error occurred. The application will continue running."
        )
#endif

    let safeDispatch (dispatch: 'msg -> unit) (msg: 'msg) =
#if DEBUG
        dispatch msg
#else
        try
            dispatch msg
        with ex ->
            eprintfn "[DISPATCH ERROR] %A" ex
            showErrorDialog "An unexpected error occurred. The application will continue running."
#endif

module Main =

    let view () =
        Component(fun ctx ->
            let writableModel = ctx.useState (ApiExplorer.init (), true)
            let model, dispatch = ctx.useElmish(writableModel, ApiExplorerUpdate.update)
            let safe = ErrorHandling.safeDispatch dispatch

            // Port polling effect
            ctx.useEffect(
                handler = (fun _ ->
                    let timer = Avalonia.Threading.DispatcherTimer()
                    timer.Interval <- TimeSpan.FromMilliseconds 1000.0
                    let mutable lastPorts : DetectedPort list = []
                    timer.Tick.Add(fun _ ->
                        let currentPorts = detectPorts ()
                        if currentPorts <> lastPorts then
                            lastPorts <- currentPorts
                            safe (ApiExplorer.PortsUpdated currentPorts)
                    )
                    timer.Start()
                    { new IDisposable with member _.Dispose() = timer.Stop() }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )

            // Loco detection timer
            ctx.useEffect(
                handler = (fun _ ->
                    let locoTimer = DispatcherTimer()
                    locoTimer.Interval <- TimeSpan.FromSeconds(1.0)
                    locoTimer.Tick.Add(fun _ ->
                        if writableModel.Current.ApiConfig.IsSome then safe ApiExplorer.DetectLoco
                    )
                    locoTimer.Start()

                    { new IDisposable with
                        member _.Dispose() = locoTimer.Stop() }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )

            ApiExplorerViews.mainView model safe
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "AWS Sunflower"
        base.Width <- 750.0
        base.Height <- 950.0
        base.WindowStartupLocation <- WindowStartupLocation.CenterScreen
        let iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "sunflower.ico")
        if System.IO.File.Exists(iconPath) then
            base.Icon <- WindowIcon(iconPath)
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
        ErrorHandling.setupGlobalExceptionHandlers ()
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
