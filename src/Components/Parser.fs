﻿namespace Atom.FSharp

open FunScript
open FunScript.TypeScript
open FunScript.TypeScript.fs
open FunScript.TypeScript.child_process
open FunScript.TypeScript.AtomCore
open FunScript.TypeScript.text_buffer
open FunScript.TypeScript.path

open Atom
open Atom.FSharp

[<ReflectedDefinition>]
module Parser =
    let private subscriptions = ResizeArray()
    let mutable private h : Disposable option = None

    let private parseProjectForEditor (editor: IEditor) =
        if JS.isDefined editor && JS.isPropertyDefined editor "buffer" && unbox<obj>(editor.buffer) <> null && JS.isPropertyDefined editor.buffer "file" && unbox<obj>(editor.buffer.file) <> null && isFSharpEditor editor then
            let rec findFsProj dir =
                if Globals.existsSync dir && Globals.lstatSync(dir).isDirectory() then
                    let files = Globals.readdirSync dir
                    let projfile = files |> Array.tryFind(fun s -> s.EndsWith(".fsproj") || s.EndsWith "project.json" )
                    match projfile with
                    | None ->
                        let parent = if dir.LastIndexOf(Globals.sep) > 0 then dir.Substring(0, dir.LastIndexOf Globals.sep) else ""
                        if System.String.IsNullOrEmpty parent then None else findFsProj parent
                    | Some p -> dir + Globals.sep + p |> Some
                else None

            let p = editor.buffer.file.path
            if JS.isDefined p then
                let res = p |> Globals.dirname
                            |> findFsProj
                match res with
                | Some r ->
                    async {
                        let! _ = LanguageService.project r
                        return ()
                    } |> Async.StartImmediate
                    true
                | None -> false
            else
                false
        else
            false

    let activate () =
        let editor = Globals.atom.workspace.getActiveTextEditor()
        parseProjectForEditor editor |> ignore
        Globals.atom.workspace.onDidChangeActivePaneItem ((fun ed ->
            ed |> parseProjectForEditor |> ignore
            h |> Option.iter(fun h' -> h'.dispose ())

        ) |> unbox<Function>
        ) |> subscriptions.Add




    let deactivate () =
        subscriptions |> Seq.iter(fun n -> n.dispose())
        subscriptions.Clear ()
        ()
