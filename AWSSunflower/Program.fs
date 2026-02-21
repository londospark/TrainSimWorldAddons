namespace CounterApp

open System
open System.IO.Ports
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Threading

module Main =

    let view () =
        Component(fun ctx ->
            let state = ctx.useState 0
            let serialPorts = ctx.useState []
            let currentSerialPort = ctx.useState None
            
            ctx.useEffect(
                handler = (fun _ ->
                    let timer = DispatcherTimer()
                    timer.Interval <- TimeSpan.FromMilliseconds 1000.0
                    
                    timer.Tick.Add(fun _ ->
                        let currentPorts = SerialPort.GetPortNames() |> List.ofArray
                        
                        if currentPorts <> serialPorts.Current then
                            serialPorts.Set currentPorts
                    )
                    
                    timer.Start()
                    
                    serialPorts.Set (SerialPort.GetPortNames() |> List.ofArray)
                    
                    { new IDisposable with
                        member _.Dispose() =
                            timer.Stop()
                    }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )

            DockPanel.create [
                DockPanel.children [
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> state.Set(state.Current - 1))
                        Button.content "-"
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> state.Set(state.Current + 1))
                        Button.content "+"
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    ComboBox.create [
                        ComboBox.dock Dock.Top
                        ComboBox.placeholderText "Select a COM port."
                        ComboBox.horizontalAlignment HorizontalAlignment.Stretch
                        ComboBox.horizontalContentAlignment HorizontalAlignment.Center
                        ComboBox.dataItems serialPorts.Current
                        ComboBox.onSelectedItemChanged (fun item -> currentSerialPort.Set(Some <| string item))
                    ]
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.fontSize 48.0
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text (currentSerialPort.Current |> Option.defaultValue "No Port")
                    ]
                ]
            ]
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "Counter Example"
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
