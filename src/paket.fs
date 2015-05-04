[<ReflectedDefinition>]
module Paket

open FunScript
open FunScript.TypeScript
open FunScript.TypeScript.child_process
open FunScript.TypeScript.AtomCore

open Atom

module PaketService =
    type Options = {cwd : string}

    let location () = Globals.atom.packages.packageDirPaths.[0] + "/paket/bin/paket.exe"
    let bootstrapperLocation () = Globals.atom.packages.packageDirPaths.[0] + "/paket/bin/paket.bootstrapper.exe"

    let handle (error : Error) (stdout : Buffer) (stderr : Buffer) =
        Globals.atom.emit("FSharp:Output", stdout.toString())
        Globals.console.log(stdout.toString())

    let exec cmd =
        let options = {cwd = Globals.atom.project.getPath()}
        if Globals._process.platform.StartsWith("win") then
            Globals.exec(cmd, unbox<AnonymousType600>options,  System.Func<_,_,_,_>(handle)) |> ignore
        else
            Globals.exec("mono" + cmd, unbox<AnonymousType600>options,  System.Func<_,_,_,_>(handle)) |> ignore


    let UpdatePaket () =
        let cmd = bootstrapperLocation()
        if Globals._process.platform.StartsWith("win") then
            Globals.exec(cmd, System.Func<_,_,_,_>(handle)) |> ignore
        else
            Globals.exec("mono" + cmd,  System.Func<_,_,_,_>(handle)) |> ignore

    let Init () = "init" |> exec
    let Install () = "install" |> exec
    let Update () = "update" |> exec
    let Outdated () = "outdated" |> exec




type Paket() =


    member x.activate(state:obj) =
        Atom.addCommand("atom-workspace", "Paket: Update Paket", PaketService.UpdatePaket)
        Atom.addCommand("atom-workspace", "Paket: Init", PaketService.Init)
        Atom.addCommand("atom-workspace", "Paket: Install", PaketService.Install)
        Atom.addCommand("atom-workspace", "Paket: Update", PaketService.Update)
        Atom.addCommand("atom-workspace", "Paket: Outdated", PaketService.Outdated)
        ()

    member x.deactivate() =
        ()